using Couchbase;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.KeyValue;
using Npgsql;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;

namespace CustomerReseed;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
        var ct = cts.Token;

        var mode = (Env.Optional("MODE") ?? "FULL_LOCAL").Trim().ToUpperInvariant(); // DOCS | EMBEDDINGS_LOCAL | FULL_LOCAL

        var pgConnString = Env.Require("PG_CONN");

        var cbConnString = Env.Require("CB_CONN");
        var cbUser = Env.Require("CB_USER");
        var cbPass = Env.Require("CB_PASS");
        var cbBucket = Env.Require("CB_BUCKET");
        var cbScope = Env.Optional("CB_SCOPE") ?? "_default";
        var cbCollection = Env.Optional("CB_COLLECTION") ?? "_default";

        var outDir = Env.Optional("OUT_DIR") ?? Path.Combine(Environment.CurrentDirectory, "batch-data");
        var embeddingModel = Env.Optional("EMBEDDING_MODEL") ?? "text-embedding-3-small";
        var embeddingDims = Env.Int("EMBEDDING_DIMS", 512);

        var pgReadBatch = Env.Int("PG_READ_BATCH", 5000);
        var docsConcurrency = Env.Int("DOCS_CONCURRENCY", 24);
        var embedConcurrency = Env.Int("EMBEDDINGS_CONCURRENCY", 32);

        var cbKvTimeoutSeconds = Env.Int("CB_KV_TIMEOUT_SECONDS", 10);
        var progressEvery = Env.Int("PROGRESS_EVERY", 10_000);

        var missingDocsLog = Env.Optional("MISSING_DOCS_LOG") ?? "missing_docids.log";
        var failedDocsLog = Env.Optional("FAILED_DOCS_LOG") ?? "failed_docs.log";
        var failedEmbedsLog = Env.Optional("FAILED_EMBEDS_LOG") ?? "failed_embeds.log";

        Console.WriteLine($"MODE={mode}");
        Console.WriteLine($"CB={cbConnString}, bucket={cbBucket}, scope={cbScope}, collection={cbCollection}, kvTimeout={cbKvTimeoutSeconds}s");
        Console.WriteLine($"OUT_DIR={outDir}, embeddingModel={embeddingModel}, embeddingDims={embeddingDims}");
        Console.WriteLine($"docsConcurrency={docsConcurrency}, embedConcurrency={embedConcurrency}, progressEvery={progressEvery}");

        if (mode is not ("DOCS" or "EMBEDDINGS_LOCAL" or "FULL_LOCAL"))
            throw new InvalidOperationException("MODE must be DOCS | EMBEDDINGS_LOCAL | FULL_LOCAL");

        Directory.CreateDirectory(outDir);

        Console.WriteLine("CB: connecting...");
        var cluster = await Cluster.ConnectAsync(cbConnString, new ClusterOptions
        {
            KvTimeout = TimeSpan.FromSeconds(cbKvTimeoutSeconds),
            KvConnectTimeout = TimeSpan.FromSeconds(30)
        }.WithCredentials(cbUser, cbPass));

        try
        {
            var bucket = await cluster.BucketAsync(cbBucket);
            var scope = await bucket.ScopeAsync(cbScope);
            var collection = await scope.CollectionAsync(cbCollection);
            Console.WriteLine("CB: connected.");

            var exitCode = 0;

            if (mode is "DOCS" or "FULL_LOCAL")
            {
                var ok = await MigrateDocsFromPostgresAsync(
                    pgConnString,
                    collection,
                    pgReadBatch,
                    docsConcurrency,
                    cbKvTimeoutSeconds,
                    progressEvery,
                    failedDocsLog,
                    ct);

                if (!ok) exitCode = 1;
            }

            if (mode is "EMBEDDINGS_LOCAL" or "FULL_LOCAL")
            {
                var ok = await ApplyEmbeddingsFromLocalBatchOutputsAsync(
                    outDir,
                    collection,
                    embeddingModel,
                    embeddingDims,
                    embedConcurrency,
                    cbKvTimeoutSeconds,
                    progressEvery,
                    missingDocsLog,
                    failedEmbedsLog,
                    ct);

                if (!ok) exitCode = 1;
            }

            return exitCode;
        }
        finally
        {
            await cluster.DisposeAsync();
        }
    }

    private static async Task<bool> MigrateDocsFromPostgresAsync(
        string pgConnString,
        ICouchbaseCollection collection,
        int pgReadBatch,
        int docsConcurrency,
        int cbKvTimeoutSeconds,
        int progressEvery,
        string failedDocsLog,
        CancellationToken ct)
    {
        Console.WriteLine("PG: connecting...");
        await using var pg = new NpgsqlConnection(pgConnString);
        await pg.OpenAsync(ct);
        Console.WriteLine("PG: connected.");

        await using var failedSink = new LineSink(failedDocsLog, reset: true);

        var channel = Channel.CreateBounded<DocWork>(new BoundedChannelOptions(Math.Max(10_000, pgReadBatch * 4))
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleWriter = true,
            SingleReader = false
        });

        long read = 0;
        long upserted = 0;
        long failed = 0;

        var sw = Stopwatch.StartNew();
        long lastReport = 0;

        Console.WriteLine($"DOCS: starting {docsConcurrency} workers...");
        var workers = Enumerable.Range(0, docsConcurrency).Select(_ => Task.Run(async () =>
        {
            await foreach (var item in channel.Reader.ReadAllAsync(ct))
            {
                try
                {
                    await collection.UpsertAsync(item.DocId, item.Doc, opts => opts.Timeout(TimeSpan.FromSeconds(cbKvTimeoutSeconds)));
                    Interlocked.Increment(ref upserted);
                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref failed);
                    await failedSink.AppendAsync($"{item.DocId}\t{ex.GetType().Name}: {ex.Message}", ct);
                }
            }
        }, ct)).ToArray();

        try
        {
            const string sql = """
                SELECT
                  id,
                  merge_key,
                  provider_no,
                  provider_name,
                  customer_number,
                  first_name,
                  last_name,
                  full_name,
                  birthdate,
                  phone,
                  email,
                  created_at,
                  updated_at
                FROM public.customers_unified
                WHERE phone IS NOT NULL OR email IS NOT NULL;
                """;

            Console.WriteLine("DOCS: streaming rows from PG...");
            await using var cmd = new NpgsqlCommand(sql, pg) { CommandTimeout = 0 };
            await using var reader = await cmd.ExecuteReaderAsync(System.Data.CommandBehavior.SequentialAccess, ct);

            while (await reader.ReadAsync(ct))
            {
                ct.ThrowIfCancellationRequested();

                var id = reader.GetGuid(0);
                var mergeKey = reader.GetString(1);
                var providerNo = reader.GetInt32(2);
                var providerName = reader.GetString(3);

                var customerNumber = reader.IsDBNull(4) ? null : reader.GetString(4);
                var first = reader.IsDBNull(5) ? null : reader.GetString(5);
                var last = reader.IsDBNull(6) ? null : reader.GetString(6);
                var full = reader.IsDBNull(7) ? null : reader.GetString(7);

                var birthdate = reader.IsDBNull(8) ? (DateTime?)null : reader.GetDateTime(8).Date;

                var phone = reader.IsDBNull(9) ? null : reader.GetString(9);
                var email = reader.IsDBNull(10) ? null : reader.GetString(10);

                var createdAt = reader.IsDBNull(11) ? (DateTime?)null : reader.GetDateTime(11);
                var updatedAt = reader.IsDBNull(12) ? (DateTime?)null : reader.GetDateTime(12);

                var fullName = !string.IsNullOrWhiteSpace(full)
                    ? full!.Trim()
                    : string.Join(' ', new[] { first, last }.Where(x => !string.IsNullOrWhiteSpace(x))).Trim();

                var emailDomain = TryGetEmailDomain(email);

                var embeddingText =
                    $"provider:{providerName} " +
                    $"name:{fullName} " +
                    $"customerNumber:{customerNumber ?? ""} " +
                    $"hasPhone:{(phone is not null ? "1" : "0")} " +
                    $"hasEmail:{(email is not null ? "1" : "0")} " +
                    $"emailDomain:{emailDomain ?? ""}";

                var docId = $"cust::p{providerNo}::{id:D}";

                var doc = new CustomerDoc
                {
                    Type = "customer",
                    Id = id,
                    MergeKey = mergeKey,
                    Provider = new Provider(providerNo, providerName),
                    CustomerNumber = NullIfEmpty(customerNumber),
                    Name = new PersonName(NullIfEmpty(first), NullIfEmpty(last), NullIfEmpty(fullName)),
                    Birthdate = birthdate?.ToString("yyyy-MM-dd"),
                    Contact = new Contact(NullIfEmpty(phone), NullIfEmpty(email)),
                    EmbeddingText = embeddingText.Trim(),
                    CreatedAt = createdAt,
                    UpdatedAt = updatedAt
                };

                await channel.Writer.WriteAsync(new DocWork(docId, doc), ct);

                var current = Interlocked.Increment(ref read);
                if (current - lastReport >= progressEvery)
                {
                    lastReport = current;
                    var elapsed = sw.Elapsed.TotalSeconds;
                    var rate = elapsed <= 0 ? 0 : upserted / elapsed;
                    Console.WriteLine($"DOCS progress: read={read}, upserted={upserted}, failed={failed}, rate={rate:0.0}/sec, elapsed={sw.Elapsed:hh\\:mm\\:ss}");
                }
            }

            channel.Writer.Complete();
            await Task.WhenAll(workers);

            Console.WriteLine($"DOCS DONE: read={read}, upserted={upserted}, failed={failed}");
            return failed == 0;
        }
        catch (OperationCanceledException)
        {
            channel.Writer.TryComplete();
            await Task.WhenAll(workers);
            Console.WriteLine("DOCS cancelled.");
            throw;
        }
    }

    private static async Task<bool> ApplyEmbeddingsFromLocalBatchOutputsAsync(
        string outDir,
        ICouchbaseCollection collection,
        string embeddingModel,
        int embeddingDims,
        int embedConcurrency,
        int cbKvTimeoutSeconds,
        int progressEvery,
        string missingDocsLog,
        string failedEmbedsLog,
        CancellationToken ct)
    {
        var outputFiles = Directory.EnumerateFiles(outDir, "output_*.jsonl", SearchOption.TopDirectoryOnly)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (outputFiles.Length == 0)
            throw new InvalidOperationException($"No output_*.jsonl found under OUT_DIR={outDir}. (Batch çıktıları indirildi mi?)");

        Console.WriteLine($"EMBEDDINGS: found {outputFiles.Length} output files.");

        await using var missingSink = new LineSink(missingDocsLog, reset: true);
        await using var failedSink = new LineSink(failedEmbedsLog, reset: true);

        var channel = Channel.CreateBounded<EmbedWork>(new BoundedChannelOptions(50_000)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleWriter = true,
            SingleReader = false
        });

        long readLines = 0;
        long ok = 0;
        long missing = 0;
        long failed = 0;

        var sw = Stopwatch.StartNew();
        long lastReport = 0;

        Console.WriteLine($"EMBEDDINGS: starting {embedConcurrency} workers...");
        var workers = Enumerable.Range(0, embedConcurrency).Select(_ => Task.Run(async () =>
        {
            await foreach (var item in channel.Reader.ReadAllAsync(ct))
            {
                try
                {
                    await collection.MutateInAsync(item.DocId,
                        specs => specs.Upsert("embedding", item.EmbeddingBase64)
                                      .Upsert("embeddingModel", embeddingModel)
                                      .Upsert("embeddingDims", embeddingDims)
                                      .Upsert("embeddedAt", DateTimeOffset.UtcNow.ToString("O")),
                        opts => opts.Timeout(TimeSpan.FromSeconds(cbKvTimeoutSeconds)));

                    Interlocked.Increment(ref ok);
                }
                catch (DocumentNotFoundException)
                {
                    Interlocked.Increment(ref missing);
                    await missingSink.AppendAsync(item.DocId, ct);
                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref failed);
                    await failedSink.AppendAsync($"{item.DocId}\t{ex.GetType().Name}: {ex.Message}", ct);
                }
            }
        }, ct)).ToArray();

        try
        {
            foreach (var file in outputFiles)
            {
                Console.WriteLine($"EMBEDDINGS: applying {Path.GetFileName(file)} ...");

                await using var fs = File.OpenRead(file);
                using var sr = new StreamReader(fs);

                string? line;
                while ((line = await sr.ReadLineAsync()) is not null)
                {
                    ct.ThrowIfCancellationRequested();
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    using var doc = JsonDocument.Parse(line);
                    var root = doc.RootElement;

                    if (!root.TryGetProperty("custom_id", out var cidEl)) continue;
                    var docId = cidEl.GetString();
                    if (string.IsNullOrWhiteSpace(docId)) continue;

                    if (!root.TryGetProperty("response", out var respEl)) continue;
                    if (!respEl.TryGetProperty("status_code", out var scEl)) continue;

                    var statusCode = scEl.GetInt32();
                    if (statusCode != 200) continue;

                    if (!respEl.TryGetProperty("body", out var bodyEl)) continue;
                    if (!bodyEl.TryGetProperty("data", out var dataEl)) continue;
                    if (dataEl.GetArrayLength() == 0) continue;

                    var embEl = dataEl[0].GetProperty("embedding");
                    var vec = new float[embEl.GetArrayLength()];
                    for (var i = 0; i < vec.Length; i++)
                        vec[i] = embEl[i].GetSingle();

                    if (vec.Length != embeddingDims)
                    {
                        Interlocked.Increment(ref failed);
                        await failedSink.AppendAsync($"{docId}\tDimsMismatch expected={embeddingDims} actual={vec.Length}", ct);
                        continue;
                    }

                    var normalized = NormalizeL2(vec);
                    var b64 = ToBase64LittleEndianFloat32(normalized);

                    await channel.Writer.WriteAsync(new EmbedWork(docId!, b64), ct);

                    var current = Interlocked.Increment(ref readLines);
                    if (current - lastReport >= progressEvery)
                    {
                        lastReport = current;
                        var elapsed = sw.Elapsed.TotalSeconds;
                        var rate = elapsed <= 0 ? 0 : ok / elapsed;
                        Console.WriteLine($"EMBEDDINGS progress: lines={readLines}, ok={ok}, missing={missing}, failed={failed}, rate={rate:0.0}/sec, elapsed={sw.Elapsed:hh\\:mm\\:ss}");
                    }
                }
            }

            channel.Writer.Complete();
            await Task.WhenAll(workers);

            Console.WriteLine($"EMBEDDINGS DONE: lines={readLines}, ok={ok}, missing={missing}, failed={failed}");
            return failed == 0;
        }
        catch (OperationCanceledException)
        {
            channel.Writer.TryComplete();
            await Task.WhenAll(workers);
            Console.WriteLine("EMBEDDINGS cancelled.");
            throw;
        }
    }

    private static float[] NormalizeL2(float[] v)
    {
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

    private static string? TryGetEmailDomain(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;
        var at = email.IndexOf('@');
        if (at < 0 || at == email.Length - 1) return null;
        return email[(at + 1)..].Trim().ToLowerInvariant();
    }

    private static string? NullIfEmpty(string? s)
        => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private sealed record DocWork(string DocId, CustomerDoc Doc);
    private sealed record EmbedWork(string DocId, string EmbeddingBase64);
}

internal static class Env
{
    public static string Require(string key)
        => Environment.GetEnvironmentVariable(key) ?? throw new InvalidOperationException($"Missing env var: {key}");

    public static string? Optional(string key) => Environment.GetEnvironmentVariable(key);

    public static int Int(string key, int def)
        => int.TryParse(Optional(key), out var v) ? v : def;
}

internal sealed record CustomerDoc
{
    public string Type { get; init; } = "customer";
    public Guid Id { get; init; }
    public string MergeKey { get; init; } = default!;
    public Provider Provider { get; init; } = default!;
    public string? CustomerNumber { get; init; }
    public PersonName Name { get; init; } = default!;
    public string? Birthdate { get; init; }
    public Contact Contact { get; init; } = default!;
    public string EmbeddingText { get; init; } = default!;
    public DateTime? CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

internal sealed record Provider(int No, string Name);
internal sealed record PersonName(string? First, string? Last, string? Full);
internal sealed record Contact(string? Phone, string? Email);

internal sealed class LineSink : IAsyncDisposable
{
    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public LineSink(string path, bool reset)
    {
        _path = path;
        if (reset && File.Exists(_path)) File.Delete(_path);
    }

    public async ValueTask AppendAsync(string line, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            await File.AppendAllTextAsync(_path, line + Environment.NewLine, ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    public ValueTask DisposeAsync()
    {
        _gate.Dispose();
        return ValueTask.CompletedTask;
    }
}