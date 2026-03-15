using System;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Couchbase;
using Couchbase.KeyValue;
using Npgsql;

namespace CustomerMigrator;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };
        var ct = cts.Token;

        var pgConnString = Env.Require("PG_CONN");

        var cbConnString = Env.Require("CB_CONN"); // couchbase://... or couchbases://...
        var cbUser = Env.Require("CB_USER");
        var cbPass = Env.Require("CB_PASS");
        var cbBucket = Env.Require("CB_BUCKET");
        var cbScope = Env.Optional("CB_SCOPE") ?? "_default";
        var cbCollection = Env.Optional("CB_COLLECTION") ?? "_default";

        var batchSize = Env.Int("BATCH_SIZE", 5000);
        var maxConcurrency = Env.Int("MAX_CONCURRENCY", 24);

        var dryRun = Env.Bool("DRY_RUN", false);
        var skipDelete = Env.Bool("SKIP_DELETE", false);

        var exportJsonlPath = Env.Optional("OUT_JSONL");
        var failedLogPath = Env.Optional("FAILED_LOG") ?? "failed_docids.log";

        var progressEvery = Env.Int("PROGRESS_EVERY", 10_000);
        var kvTimeoutSeconds = Env.Int("CB_KV_TIMEOUT_SECONDS", 10);
        var kvConnectTimeoutSeconds = Env.Int("CB_KV_CONNECT_TIMEOUT_SECONDS", 30);

        Console.WriteLine($"dryRun={dryRun}, skipDelete={skipDelete}, batchSize={batchSize}, maxConcurrency={maxConcurrency}");
        Console.WriteLine($"Couchbase bucket={cbBucket}, scope={cbScope}, collection={cbCollection}, kvTimeoutSeconds={kvTimeoutSeconds}, kvConnectTimeoutSeconds={kvConnectTimeoutSeconds}");
        Console.WriteLine($"failedLog={failedLogPath}");
        if (!string.IsNullOrWhiteSpace(exportJsonlPath))
            Console.WriteLine($"JSONL export enabled: {exportJsonlPath}");

        Console.WriteLine("PG: opening connection...");
        await using var pg = new NpgsqlConnection(pgConnString);
        await pg.OpenAsync(ct);
        Console.WriteLine("PG: connected.");

        if (!dryRun && !skipDelete)
        {
            Console.WriteLine("PG: deleting rows where phone+email null...");
            await using var delCmd = new NpgsqlCommand("""
                DELETE FROM public.customers_unified
                WHERE phone IS NULL AND email IS NULL;
                """, pg);

            delCmd.CommandTimeout = 0;
            var deleted = await delCmd.ExecuteNonQueryAsync(ct);
            Console.WriteLine($"PG: delete done, deleted={deleted}");
        }
        else
        {
            Console.WriteLine("PG: skipping DELETE step (DRY_RUN or SKIP_DELETE).");
        }

        ICluster? cluster = null;
        ICouchbaseCollection? collection = null;

        if (!dryRun)
        {
            Console.WriteLine("CB: connecting...");
            var options = new ClusterOptions
            {
                KvTimeout = TimeSpan.FromSeconds(kvTimeoutSeconds),
                KvConnectTimeout = TimeSpan.FromSeconds(kvConnectTimeoutSeconds)
            }.WithCredentials(cbUser, cbPass);

            cluster = await Cluster.ConnectAsync(cbConnString, options);

            var bucket = await cluster.BucketAsync(cbBucket);
            var scope = await bucket.ScopeAsync(cbScope);
            collection = await scope.CollectionAsync(cbCollection);

            Console.WriteLine("CB: connected, collection ready.");
        }
        else
        {
            Console.WriteLine("CB: DRY_RUN enabled, skipping Couchbase connection.");
        }

        StreamWriter? jsonlWriter = null;
        if (!string.IsNullOrWhiteSpace(exportJsonlPath))
        {
            jsonlWriter = new StreamWriter(File.Open(exportJsonlPath, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                AutoFlush = true
            };
        }

        await using var failedSink = new FailedSink(failedLogPath);

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };

        var channel = Channel.CreateBounded<WorkItem>(new BoundedChannelOptions(Math.Max(10_000, batchSize * 4))
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleWriter = true,
            SingleReader = false
        });

        long readCount = 0;
        long upsertedCount = 0;
        long failedCount = 0;

        var sw = Stopwatch.StartNew();
        long lastReport = 0;

        Console.WriteLine($"Workers: starting {maxConcurrency} workers...");
        var workers = Enumerable.Range(0, maxConcurrency).Select(_ => Task.Run(async () =>
        {
            if (dryRun)
            {
                await foreach (var _ in channel.Reader.ReadAllAsync(ct))
                    Interlocked.Increment(ref upsertedCount);

                return;
            }

            var coll = collection!;
            await foreach (var item in channel.Reader.ReadAllAsync(ct))
            {
                var ok = await UpsertWithRetry(coll, item.DocId, item.Doc, kvTimeoutSeconds, ct);

                if (ok)
                {
                    Interlocked.Increment(ref upsertedCount);
                }
                else
                {
                    Interlocked.Increment(ref failedCount);
                    await failedSink.AppendAsync(item.DocId, ct);
                }
            }
        }, ct)).ToArray();

        try
        {
            Console.WriteLine("PG: starting read stream...");

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

            await using var cmd = new NpgsqlCommand(sql, pg)
            {
                CommandTimeout = 0
            };

            await using var reader = await cmd.ExecuteReaderAsync(
                System.Data.CommandBehavior.SequentialAccess,
                ct
            );

            Console.WriteLine("PG: reader opened, streaming rows...");

            while (await reader.ReadAsync(ct))
            {
                var row = new CustomerRow(
                    Id: reader.GetGuid(0),
                    MergeKey: reader.GetString(1),
                    ProviderNo: reader.GetInt32(2),
                    ProviderName: reader.GetString(3),
                    CustomerNumber: reader.IsDBNull(4) ? null : reader.GetString(4),
                    FirstName: reader.IsDBNull(5) ? null : reader.GetString(5),
                    LastName: reader.IsDBNull(6) ? null : reader.GetString(6),
                    FullName: reader.IsDBNull(7) ? null : reader.GetString(7),
                    Birthdate: reader.IsDBNull(8) ? null : reader.GetDateTime(8).Date,
                    Phone: reader.IsDBNull(9) ? null : reader.GetString(9),
                    Email: reader.IsDBNull(10) ? null : reader.GetString(10),
                    CreatedAt: reader.IsDBNull(11) ? (DateTime?)null : reader.GetDateTime(11),
                    UpdatedAt: reader.IsDBNull(12) ? (DateTime?)null : reader.GetDateTime(12)
                );

                var doc = CustomerDoc.FromRow(row);
                var docId = $"cust::p{row.ProviderNo}::{row.Id:D}";

                if (jsonlWriter is not null)
                {
                    var line = JsonSerializer.Serialize(new { id = docId, doc }, jsonOptions);
                    await jsonlWriter.WriteLineAsync(line);
                }

                await channel.Writer.WriteAsync(new WorkItem(docId, doc), ct);

                var current = Interlocked.Increment(ref readCount);
                if (current - lastReport >= progressEvery)
                {
                    lastReport = current;
                    var elapsed = sw.Elapsed.TotalSeconds;
                    var rate = elapsed <= 0 ? 0 : upsertedCount / elapsed;

                    Console.WriteLine($"Progress: read={readCount}, upserted={upsertedCount}, failed={failedCount}, rate={rate:0.0}/sec, elapsed={sw.Elapsed:hh\\:mm\\:ss}");
                }
            }

            Console.WriteLine("PG: stream completed, completing channel...");
            channel.Writer.Complete();
            await Task.WhenAll(workers);
        }
        catch (OperationCanceledException)
        {
            channel.Writer.TryComplete();
            await Task.WhenAll(workers);
            Console.WriteLine("Cancelled.");
            return 2;
        }
        finally
        {
            if (jsonlWriter is not null)
            {
                await jsonlWriter.FlushAsync();
                await jsonlWriter.DisposeAsync();
            }

            if (cluster is not null)
                await cluster.DisposeAsync();
        }

        Console.WriteLine($"DONE: read={readCount}, upserted={upsertedCount}, failed={failedCount}, elapsed={sw.Elapsed:hh\\:mm\\:ss}");
        return failedCount > 0 ? 1 : 0;
    }

    private static async Task<bool> UpsertWithRetry(
        ICouchbaseCollection collection,
        string docId,
        CustomerDoc doc,
        int kvTimeoutSeconds,
        CancellationToken ct)
    {
        const int maxAttempts = 7;
        const int baseDelayMs = 200;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                await collection.UpsertAsync(docId, doc, options =>
                {
                    options.Timeout(TimeSpan.FromSeconds(kvTimeoutSeconds));
                });

                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (attempt < maxAttempts && IsRetryable(ex))
            {
                var expDelay = Math.Min(10_000, baseDelayMs * (1 << (attempt - 1)));
                var jitter = Random.Shared.Next(0, 250);
                await Task.Delay(expDelay + jitter, ct);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Upsert failed docId={docId}, attempt={attempt}/{maxAttempts}, err={ex.GetType().Name}: {ex.Message}");
                return false;
            }
        }

        return false;
    }

    private static bool IsRetryable(Exception ex)
        => ex is TimeoutException || ex is CouchbaseException;
}

internal static class Env
{
    public static string Require(string key)
        => Environment.GetEnvironmentVariable(key) ?? throw new InvalidOperationException($"Missing env var: {key}");

    public static string? Optional(string key) => Environment.GetEnvironmentVariable(key);

    public static int Int(string key, int def)
        => int.TryParse(Optional(key), out var v) ? v : def;

    public static bool Bool(string key, bool def)
        => bool.TryParse(Optional(key), out var v) ? v : def;
}

internal sealed record WorkItem(string DocId, CustomerDoc Doc);

internal sealed record CustomerRow(
    Guid Id,
    string MergeKey,
    int ProviderNo,
    string ProviderName,
    string? CustomerNumber,
    string? FirstName,
    string? LastName,
    string? FullName,
    DateTime? Birthdate,
    string? Phone,
    string? Email,
    DateTime? CreatedAt,
    DateTime? UpdatedAt
);

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

    public static CustomerDoc FromRow(CustomerRow r)
    {
        var fullName = !string.IsNullOrWhiteSpace(r.FullName)
            ? r.FullName!.Trim()
            : string.Join(' ', new[] { r.FirstName, r.LastName }.Where(x => !string.IsNullOrWhiteSpace(x))).Trim();

        var embeddingText =
            $"provider:{r.ProviderName} " +
            $"customerNumber:{r.CustomerNumber ?? ""} " +
            $"name:{fullName} " +
            $"email:{r.Email ?? ""} " +
            $"phone:{r.Phone ?? ""}";

        return new CustomerDoc
        {
            Id = r.Id,
            MergeKey = r.MergeKey,
            Provider = new Provider(r.ProviderNo, r.ProviderName),
            CustomerNumber = NullIfEmpty(r.CustomerNumber),
            Name = new PersonName(NullIfEmpty(r.FirstName), NullIfEmpty(r.LastName), NullIfEmpty(fullName)),
            Birthdate = r.Birthdate?.ToString("yyyy-MM-dd"),
            Contact = new Contact(NullIfEmpty(r.Phone), NullIfEmpty(r.Email)),
            EmbeddingText = embeddingText.Trim(),
            CreatedAt = r.CreatedAt,
            UpdatedAt = r.UpdatedAt
        };
    }

    private static string? NullIfEmpty(string? s)
        => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}

internal sealed record Provider(int No, string Name);
internal sealed record PersonName(string? First, string? Last, string? Full);
internal sealed record Contact(string? Phone, string? Email);

internal sealed class FailedSink : IAsyncDisposable
{
    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public FailedSink(string path)
    {
        _path = path;
        if (File.Exists(_path))
            File.Delete(_path);
    }

    public async ValueTask AppendAsync(string docId, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            await File.AppendAllTextAsync(_path, docId + Environment.NewLine, ct);
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