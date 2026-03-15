using Couchbase;
using Couchbase.KeyValue;
using Npgsql;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;

namespace CustomerVectorBatcher;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
        var ct = cts.Token;

        var mode = (Env.Optional("MODE") ?? "FULL").Trim().ToUpperInvariant(); // SUBMIT | APPLY | FULL

        var pgConn = Env.Require("PG_CONN");

        var cbConn = Env.Require("CB_CONN");
        var cbUser = Env.Require("CB_USER");
        var cbPass = Env.Require("CB_PASS");
        var cbBucket = Env.Require("CB_BUCKET");
        var cbScope = Env.Optional("CB_SCOPE") ?? "_default";
        var cbCollection = Env.Optional("CB_COLLECTION") ?? "_default";
        var cbKvTimeoutSeconds = Env.Int("CB_KV_TIMEOUT_SECONDS", 10);
        var cbWriteConcurrency = Env.Int("CB_WRITE_CONCURRENCY", 32);

        var openAiKey = Env.Require("OPENAI_API_KEY");
        var embeddingModel = Env.Optional("EMBEDDING_MODEL") ?? "text-embedding-3-small";
        var embeddingDims = Env.Int("EMBEDDING_DIMS", 512); // maliyet/storage için düşük tutuyoruz
        var requestsPerBatch = Env.Int("REQUESTS_PER_BATCH", 50_000); // /v1/embeddings batch cap :contentReference[oaicite:7]{index=7}
        var outDir = Env.Optional("OUT_DIR") ?? Path.Combine(Environment.CurrentDirectory, "batch-data");

        var pollSeconds = Env.Int("POLL_SECONDS", 30);
        var progressEvery = Env.Int("PROGRESS_EVERY", 10_000);

        Directory.CreateDirectory(outDir);

        var manifestPath = Path.Combine(outDir, "manifest.json");
        var manifest = BatchManifest.Load(manifestPath);

        Console.WriteLine($"MODE={mode}");
        Console.WriteLine($"OpenAI model={embeddingModel}, dims={embeddingDims}, requestsPerBatch={requestsPerBatch}");
        Console.WriteLine($"OUT_DIR={outDir}");
        Console.WriteLine($"CB bucket={cbBucket}, scope={cbScope}, collection={cbCollection}, writeConcurrency={cbWriteConcurrency}");

        using var openAi = new OpenAiBatchClient(openAiKey);

        if (mode is "SUBMIT" or "FULL")
        {
            Console.WriteLine("SUBMIT: PG stream -> JSONL -> upload -> create batch ...");

            await using var pg = new NpgsqlConnection(pgConn);
            await pg.OpenAsync(ct);

            var batchIndex = manifest.Entries.Count == 0 ? 1 : (manifest.Entries.Max(e => e.BatchIndex) + 1);

            var currentCount = 0;
            StreamWriter? writer = null;
            string? currentFile = null;

            void StartNewFile()
            {
                currentCount = 0;
                currentFile = Path.Combine(outDir, $"embeddings_batch_{batchIndex:000}.jsonl");
                writer = new StreamWriter(File.Open(currentFile, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    AutoFlush = true
                };
                Console.WriteLine($"Creating {Path.GetFileName(currentFile)} ...");
            }

            async Task CloseAndSubmitCurrentFileAsync()
            {
                if (writer is null || currentFile is null || currentCount == 0) return;

                await writer.FlushAsync();
                writer.Dispose();
                writer = null;

                Console.WriteLine($"Uploading {Path.GetFileName(currentFile)} to OpenAI files (purpose=batch) ...");
                var fileId = await openAi.UploadBatchFileAsync(currentFile, ct);

                Console.WriteLine($"Creating OpenAI batch for {Path.GetFileName(currentFile)} ...");
                var batch = await openAi.CreateEmbeddingsBatchAsync(fileId, ct);

                manifest.Entries.Add(new BatchEntry
                {
                    BatchIndex = batchIndex,
                    InputJsonlPath = currentFile,
                    OpenAiInputFileId = fileId,
                    OpenAiBatchId = batch.Id,
                    Status = batch.Status,
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                    RequestCount = currentCount
                });

                manifest.Save(manifestPath);
                Console.WriteLine($"Submitted batchId={batch.Id}, status={batch.Status}");

                batchIndex++;
            }

            StartNewFile();

            const string sql = """
                SELECT
                  id,
                  provider_no,
                  provider_name,
                  customer_number,
                  first_name,
                  last_name,
                  full_name,
                  phone,
                  email
                FROM public.customers_unified
                WHERE phone IS NOT NULL OR email IS NOT NULL;
                """;

            await using var cmd = new NpgsqlCommand(sql, pg) { CommandTimeout = 0 };
            await using var reader = await cmd.ExecuteReaderAsync(System.Data.CommandBehavior.SequentialAccess, ct);

            long read = 0;

            while (await reader.ReadAsync(ct))
            {
                ct.ThrowIfCancellationRequested();

                var id = reader.GetGuid(0);
                var providerNo = reader.GetInt32(1);
                var providerName = reader.GetString(2);

                var customerNumber = reader.IsDBNull(3) ? null : reader.GetString(3);
                var first = reader.IsDBNull(4) ? null : reader.GetString(4);
                var last = reader.IsDBNull(5) ? null : reader.GetString(5);
                var full = reader.IsDBNull(6) ? null : reader.GetString(6);
                var phone = reader.IsDBNull(7) ? null : reader.GetString(7);
                var email = reader.IsDBNull(8) ? null : reader.GetString(8);

                var fullName = !string.IsNullOrWhiteSpace(full)
                    ? full!.Trim()
                    : string.Join(' ', new[] { first, last }.Where(x => !string.IsNullOrWhiteSpace(x))).Trim();

                // Embedding input'u kısa tutuyoruz (token maliyeti düşsün)
                // Not: phone/email'i embedding'e ham basmıyoruz, semantik aramada zaten işe yaramaz, risk getirir.
                var emailDomain = TryGetEmailDomain(email);
                var embeddingInput =
                    $"provider:{providerName} " +
                    $"name:{fullName} " +
                    $"customerNumber:{customerNumber ?? ""} " +
                    $"hasPhone:{(phone is not null ? "1" : "0")} " +
                    $"hasEmail:{(email is not null ? "1" : "0")} " +
                    $"emailDomain:{emailDomain ?? ""}";

                var docId = $"cust::p{providerNo}::{id:D}";

                var task = new
                {
                    custom_id = docId,
                    method = "POST",
                    url = "/v1/embeddings",
                    body = new
                    {
                        model = embeddingModel,
                        input = embeddingInput,
                        encoding_format = "float",
                        dimensions = embeddingDims
                    }
                };

                await writer!.WriteLineAsync(JsonSerializer.Serialize(task));

                currentCount++;
                read++;

                if (read % progressEvery == 0)
                    Console.WriteLine($"SUBMIT progress: read={read}, currentFileRequests={currentCount}, submittedBatches={manifest.Entries.Count}");

                if (currentCount >= requestsPerBatch)
                {
                    await CloseAndSubmitCurrentFileAsync();
                    StartNewFile();
                }
            }

            await CloseAndSubmitCurrentFileAsync();
            writer?.Dispose();

            Console.WriteLine($"SUBMIT done. Total batches={manifest.Entries.Count}");
        }

        if (mode is "APPLY" or "FULL")
        {
            Console.WriteLine("APPLY: polling batches, downloading outputs, writing embeddings to Couchbase as base64 ...");

            // Couchbase connect (apply aşamasında lazım)
            var cluster = await Cluster.ConnectAsync(cbConn, new ClusterOptions
            {
                KvTimeout = TimeSpan.FromSeconds(cbKvTimeoutSeconds),
                KvConnectTimeout = TimeSpan.FromSeconds(30)
            }.WithCredentials(cbUser, cbPass));

            var bucket = await cluster.BucketAsync(cbBucket);
            var scope = await bucket.ScopeAsync(cbScope);
            var collection = await scope.CollectionAsync(cbCollection);

            try
            {
                var pending = manifest.Entries.Any(e => !e.Applied && e.OpenAiBatchId is not null);
                while (pending)
                {
                    ct.ThrowIfCancellationRequested();

                    foreach (var entry in manifest.Entries.Where(e => !e.Applied && !string.IsNullOrWhiteSpace(e.OpenAiBatchId)).ToList())
                    {
                        var batch = await openAi.RetrieveBatchAsync(entry.OpenAiBatchId!, ct);
                        entry.Status = batch.Status;
                        entry.OpenAiOutputFileId = batch.OutputFileId;
                        entry.OpenAiErrorFileId = batch.ErrorFileId;
                        manifest.Save(manifestPath);

                        if (batch.Status == "completed" && !string.IsNullOrWhiteSpace(batch.OutputFileId))
                        {
                            Console.WriteLine($"Batch {entry.BatchIndex:000} completed. Downloading output file...");

                            var outputPath = Path.Combine(outDir, $"output_{entry.BatchIndex:000}.jsonl");
                            await openAi.DownloadFileContentAsync(batch.OutputFileId!, outputPath, ct);

                            Console.WriteLine($"Applying embeddings from {Path.GetFileName(outputPath)} ...");
                            var appliedOk = await ApplyOutputFileAsync(
                                outputPath,
                                collection,
                                embeddingModel,
                                embeddingDims,
                                cbWriteConcurrency,
                                cbKvTimeoutSeconds,
                                progressEvery,
                                ct);

                            entry.Applied = appliedOk;
                            entry.AppliedAtUtc = DateTimeOffset.UtcNow;
                            manifest.Save(manifestPath);

                            Console.WriteLine($"Batch {entry.BatchIndex:000} applied={appliedOk}");
                        }

                        if (batch.Status is "failed" or "cancelled" or "expired")
                        {
                            Console.WriteLine($"Batch {entry.BatchIndex:000} ended with status={batch.Status}. error_file_id={batch.ErrorFileId ?? "-"}");
                        }
                    }

                    pending = manifest.Entries.Any(e => !e.Applied && e.Status != "failed" && e.Status != "cancelled" && e.Status != "expired");
                    if (!pending) break;

                    Console.WriteLine($"Polling again in {pollSeconds}s ...");
                    await Task.Delay(TimeSpan.FromSeconds(pollSeconds), ct);
                }

                Console.WriteLine("APPLY done.");
            }
            finally
            {
                await cluster.DisposeAsync();
            }
        }

        Console.WriteLine("ALL DONE.");
        return 0;
    }

    private static async Task<bool> ApplyOutputFileAsync(
        string outputJsonlPath,
        ICouchbaseCollection collection,
        string embeddingModel,
        int embeddingDims,
        int writeConcurrency,
        int kvTimeoutSeconds,
        int progressEvery,
        CancellationToken ct)
    {
        // Batch output format: her satırda custom_id + response.body... :contentReference[oaicite:8]{index=8}
        // Embedding response body: data[0].embedding (float array) :contentReference[oaicite:9]{index=9}
        // Biz Couchbase'e base64 (little-endian float32) yazacağız, index'te vector_base64 kullanacağız. :contentReference[oaicite:10]{index=10}

        var channel = System.Threading.Channels.Channel.CreateBounded<(string DocId, float[] Vector)>(new BoundedChannelOptions(50_000)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleWriter = true,
            SingleReader = false
        });

        long read = 0, ok = 0, failed = 0;

        var workers = Enumerable.Range(0, writeConcurrency).Select(_ => Task.Run(async () =>
        {
            await foreach (var item in channel.Reader.ReadAllAsync(ct))
            {
                try
                {
                    var normalized = NormalizeL2(item.Vector);
                    var b64 = ToBase64LittleEndianFloat32(normalized);

                    await collection.MutateInAsync(item.DocId, specs =>
                            specs.Upsert("embedding", b64)
                                 .Upsert("embeddingModel", embeddingModel)
                                 .Upsert("embeddingDims", embeddingDims)
                                 .Upsert("embeddedAt", DateTimeOffset.UtcNow.ToString("O")),
                        options => options.Timeout(TimeSpan.FromSeconds(kvTimeoutSeconds)));

                    Interlocked.Increment(ref ok);
                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref failed);
                    Console.WriteLine($"CB write failed docId={item.DocId}, err={ex.GetType().Name}: {ex.Message}");
                }
            }
        }, ct)).ToArray();

        try
        {
            using var fs = File.OpenRead(outputJsonlPath);
            using var sr = new StreamReader(fs);

            string? line;
            while ((line = await sr.ReadLineAsync()) is not null)
            {
                ct.ThrowIfCancellationRequested();

                if (string.IsNullOrWhiteSpace(line)) continue;

                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;

                var customId = root.GetProperty("custom_id").GetString();
                if (string.IsNullOrWhiteSpace(customId)) continue;

                if (!root.TryGetProperty("response", out var resp)) continue;

                var statusCode = resp.TryGetProperty("status_code", out var sc) ? sc.GetInt32() : 0;
                if (statusCode != 200) continue;

                var body = resp.GetProperty("body");
                var data = body.GetProperty("data");
                if (data.GetArrayLength() == 0) continue;

                var emb = data[0].GetProperty("embedding");
                var vec = new float[emb.GetArrayLength()];
                for (var i = 0; i < vec.Length; i++)
                    vec[i] = emb[i].GetSingle();

                await channel.Writer.WriteAsync((customId!, vec), ct);

                var current = Interlocked.Increment(ref read);
                if (current % progressEvery == 0)
                    Console.WriteLine($"APPLY progress: readLines={read}, ok={ok}, failed={failed}");
            }

            channel.Writer.Complete();
            await Task.WhenAll(workers);

            Console.WriteLine($"APPLY file done: readLines={read}, ok={ok}, failed={failed}");
            return failed == 0;
        }
        catch (OperationCanceledException)
        {
            channel.Writer.TryComplete();
            await Task.WhenAll(workers);
            throw;
        }
    }

    private static string? TryGetEmailDomain(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;
        var at = email.IndexOf('@');
        if (at < 0 || at == email.Length - 1) return null;
        return email[(at + 1)..].Trim().ToLowerInvariant();
    }

    private static float[] NormalizeL2(float[] v)
    {
        // OpenAI embeddings dokümanında L2 normalize örneği var, cosine için pratikte iyi çalışır. :contentReference[oaicite:11]{index=11}
        double sum = 0;
        for (var i = 0; i < v.Length; i++) sum += (double)v[i] * v[i];
        var norm = Math.Sqrt(sum);
        if (norm <= 0) return v;

        var outV = new float[v.Length];
        for (var i = 0; i < v.Length; i++) outV[i] = (float)(v[i] / norm);
        return outV;
    }

    private static string ToBase64LittleEndianFloat32(float[] v)
    {
        var bytes = new byte[v.Length * 4];
        for (var i = 0; i < v.Length; i++)
        {
            var bits = BitConverter.SingleToInt32Bits(v[i]);
            bytes[i * 4 + 0] = (byte)(bits & 0xFF);
            bytes[i * 4 + 1] = (byte)((bits >> 8) & 0xFF);
            bytes[i * 4 + 2] = (byte)((bits >> 16) & 0xFF);
            bytes[i * 4 + 3] = (byte)((bits >> 24) & 0xFF);
        }

        return Convert.ToBase64String(bytes);
    }
}

internal static class Env
{
    public static string Require(string key)
        => Environment.GetEnvironmentVariable(key) ?? throw new InvalidOperationException($"Missing env var: {key}");

    public static string? Optional(string key) => Environment.GetEnvironmentVariable(key);

    public static int Int(string key, int def)
        => int.TryParse(Optional(key), out var v) ? v : def;
}

internal sealed class OpenAiBatchClient : IDisposable
{
    private readonly HttpClient _http;

    public OpenAiBatchClient(string apiKey)
    {
        _http = new HttpClient
        {
            BaseAddress = new Uri("https://api.openai.com/v1/"),
            Timeout = TimeSpan.FromMinutes(10)
        };
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    }

    public async Task<string> UploadBatchFileAsync(string path, CancellationToken ct)
    {
        // Files API: POST /files, purpose=batch :contentReference[oaicite:12]{index=12}
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("batch"), "purpose");

        var fileStream = File.OpenRead(path);
        var fileContent = new StreamContent(fileStream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(fileContent, "file", Path.GetFileName(path));

        using var resp = await _http.PostAsync("files", form, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        resp.EnsureSuccessStatusCode();

        using var json = JsonDocument.Parse(body);
        return json.RootElement.GetProperty("id").GetString()!;
    }

    public async Task<OpenAiBatch> CreateEmbeddingsBatchAsync(string inputFileId, CancellationToken ct)
    {
        // Batches: POST /batches, endpoint=/v1/embeddings, completion_window=24h :contentReference[oaicite:13]{index=13}
        var payload = JsonSerializer.Serialize(new
        {
            input_file_id = inputFileId,
            endpoint = "/v1/embeddings",
            completion_window = "24h"
        });

        using var resp = await _http.PostAsync("batches", new StringContent(payload, Encoding.UTF8, "application/json"), ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        resp.EnsureSuccessStatusCode();

        return OpenAiBatch.FromJson(body);
    }

    public async Task<OpenAiBatch> RetrieveBatchAsync(string batchId, CancellationToken ct)
    {
        using var resp = await _http.GetAsync($"batches/{batchId}", ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        resp.EnsureSuccessStatusCode();
        return OpenAiBatch.FromJson(body);
    }

    public async Task DownloadFileContentAsync(string fileId, string destPath, CancellationToken ct)
    {
        // GET /files/{file_id}/content :contentReference[oaicite:14]{index=14}
        using var resp = await _http.GetAsync($"files/{fileId}/content", HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();

        await using var fs = File.Open(destPath, FileMode.Create, FileAccess.Write, FileShare.Read);
        await resp.Content.CopyToAsync(fs, ct);
    }

    public void Dispose() => _http.Dispose();
}

internal sealed record OpenAiBatch(string Id, string Status, string? OutputFileId, string? ErrorFileId)
{
    public static OpenAiBatch FromJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        return new OpenAiBatch(
            Id: root.GetProperty("id").GetString()!,
            Status: root.GetProperty("status").GetString()!,
            OutputFileId: root.TryGetProperty("output_file_id", out var of) && of.ValueKind != JsonValueKind.Null ? of.GetString() : null,
            ErrorFileId: root.TryGetProperty("error_file_id", out var ef) && ef.ValueKind != JsonValueKind.Null ? ef.GetString() : null
        );
    }
}

internal sealed class BatchManifest
{
    [JsonPropertyName("entries")]
    public List<BatchEntry> Entries { get; set; } = new();

    public static BatchManifest Load(string path)
    {
        if (!File.Exists(path)) return new BatchManifest();
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<BatchManifest>(json) ?? new BatchManifest();
    }

    public void Save(string path)
    {
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }
}

internal sealed class BatchEntry
{
    public int BatchIndex { get; set; }
    public string InputJsonlPath { get; set; } = default!;
    public string? OpenAiInputFileId { get; set; }
    public string? OpenAiBatchId { get; set; }
    public string? OpenAiOutputFileId { get; set; }
    public string? OpenAiErrorFileId { get; set; }
    public string Status { get; set; } = "created";
    public int RequestCount { get; set; }
    public bool Applied { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? AppliedAtUtc { get; set; }
}