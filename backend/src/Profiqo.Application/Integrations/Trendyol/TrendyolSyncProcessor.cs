using System.Text.Json;

using Microsoft.Extensions.Options;

using Profiqo.Application.Abstractions.Crypto;
using Profiqo.Application.Abstractions.Integrations.Trendyol;
using Profiqo.Application.Abstractions.Persistence;
using Profiqo.Application.Abstractions.Persistence.Repositories;
using Profiqo.Domain.Common.Ids;
using Profiqo.Domain.Integrations;

namespace Profiqo.Application.Integrations.Trendyol;

public sealed class TrendyolSyncProcessor : ITrendyolSyncProcessor
{
    private const string CursorKey = "trendyol.orders.cursor.startDateMs";

    private readonly IProviderConnectionRepository _connections;
    private readonly ISecretProtector _secrets;
    private readonly ITrendyolClient _client;
    private readonly ITrendyolSyncStore _store;
    private readonly IIntegrationJobRepository _jobs;
    private readonly IIntegrationCursorRepository _cursors;
    private readonly TrendyolOptions _opts;

    public TrendyolSyncProcessor(
        IProviderConnectionRepository connections,
        ISecretProtector secrets,
        ITrendyolClient client,
        ITrendyolSyncStore store,
        IIntegrationJobRepository jobs,
        IIntegrationCursorRepository cursors,
        IOptions<TrendyolOptions> opts)
    {
        _connections = connections;
        _secrets = secrets;
        _client = client;
        _store = store;
        _jobs = jobs;
        _cursors = cursors;
        _opts = opts.Value;
    }

    public async Task<int> SyncOrdersAsync(Guid jobId, TenantId tenantId, Guid connectionId, int pageSize, int maxPages, CancellationToken ct)
    {
        var conn = await _connections.GetByIdAsync(new ProviderConnectionId(connectionId), ct);
        if (conn is null || conn.TenantId != tenantId || conn.ProviderType != ProviderType.Trendyol)
            throw new InvalidOperationException("Trendyol connection not found for tenant.");

        var credsJson = _secrets.Unprotect(conn.AccessToken);
        var creds = JsonSerializer.Deserialize<TrendyolCreds>(credsJson) ?? throw new InvalidOperationException("Trendyol credentials invalid.");
        var supplierId = conn.ExternalAccountId ?? throw new InvalidOperationException("Trendyol supplierId missing.");

        var cursor = await _cursors.GetAsync(tenantId, conn.Id, CursorKey, ct);
        long? startMs = long.TryParse(cursor, out var ms) && ms > 0 ? ms : null;

        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var initialStart = DateTimeOffset.UtcNow.AddDays(-_opts.InitialBackfillDays).ToUnixTimeMilliseconds();
        var effectiveStart = startMs ?? initialStart;
        var endMs = nowMs;

        var status = string.IsNullOrWhiteSpace(_opts.DefaultStatus) ? "Created" : _opts.DefaultStatus;

        var processed = 0;
        long maxSeenOrderDate = effectiveStart;

        for (var page = 0; page < maxPages; page++)
        {
            ct.ThrowIfCancellationRequested();

            using var doc = await _client.GetOrdersAsync(
                apiKey: creds.ApiKey,
                apiSecret: creds.ApiSecret,
                supplierId: supplierId,
                page: page,
                size: pageSize,
                status: status,
                startDateMs: effectiveStart,
                endDateMs: endMs,
                ct: ct);

            var root = doc.RootElement;

            // defensive: some responses nest under "content"
            var arr = FindArray(root, new[] { "content", "items", "data", "orders" });
            if (arr is null || arr.Value.GetArrayLength() == 0)
                break;

            foreach (var o in arr.Value.EnumerateArray())
            {
                ct.ThrowIfCancellationRequested();

                var providerOrderId =
                    ReadString(o, "orderNumber") ??
                    ReadString(o, "id") ??
                    Guid.NewGuid().ToString("N");

                var orderDateMs =
                    ReadInt64(o, "orderDate") ??
                    ReadInt64(o, "orderDateMs") ??
                    ReadInt64(o, "orderCreatedDate") ??
                    effectiveStart;

                if (orderDateMs > maxSeenOrderDate) maxSeenOrderDate = orderDateMs;

                var placedAtUtc = DateTimeOffset.FromUnixTimeMilliseconds(orderDateMs);

                var currency = ReadString(o, "currencyCode") ?? "TRY";
                var total = ReadDecimal(o, "totalPrice") ?? ReadDecimal(o, "totalAmount") ?? 0m;

                var email = ReadString(o, "customerEmail") ?? ReadString(o, "email");
                var phone = ReadString(o, "customerPhone") ?? ReadString(o, "phone");

                var lines = new List<TrendyolOrderLineUpsert>();

                var lineArr = FindArray(o, new[] { "lines", "orderLines", "items" });
                if (lineArr.HasValue)
                {
                    foreach (var li in lineArr.Value.EnumerateArray())
                    {
                        var sku = ReadString(li, "barcode") ?? ReadString(li, "sku") ?? ReadString(li, "merchantSku");
                        var name = ReadString(li, "productName") ?? ReadString(li, "name") ?? "unknown";
                        var qty = (int)(ReadInt64(li, "quantity") ?? 1);
                        var unit = ReadDecimal(li, "price") ?? ReadDecimal(li, "unitPrice") ?? 0m;
                        var lineTotal = ReadDecimal(li, "totalPrice") ?? (unit * qty);

                        lines.Add(new TrendyolOrderLineUpsert(sku, name, qty, unit, lineTotal, currency));
                    }
                }

                var model = new TrendyolOrderUpsert(
                    ProviderOrderId: providerOrderId,
                    PlacedAtUtc: placedAtUtc,
                    CurrencyCode: currency,
                    TotalAmount: total,
                    CustomerEmail: email,
                    CustomerPhone: phone,
                    Lines: lines,
                    PayloadJson: o.GetRawText());

                await _store.UpsertOrderAsync(tenantId, model, ct);

                processed++;
                if (processed % 50 == 0)
                    await _jobs.MarkProgressAsync(jobId, processed, ct);
            }

            await _jobs.MarkProgressAsync(jobId, processed, ct);
        }

        var nextCursor = maxSeenOrderDate > 0 ? (maxSeenOrderDate + 1) : (nowMs + 1);
        await _cursors.UpsertAsync(tenantId, conn.Id, CursorKey, nextCursor.ToString(), DateTimeOffset.UtcNow, ct);

        return processed;
    }

    private sealed record TrendyolCreds(string ApiKey, string ApiSecret);

    private static JsonElement? FindArray(JsonElement root, string[] keys)
    {
        foreach (var k in keys)
        {
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty(k, out var p) && p.ValueKind == JsonValueKind.Array)
                return p;
        }
        return null;
    }

    private static string? ReadString(JsonElement obj, string name)
        => obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private static long? ReadInt64(JsonElement obj, string name)
        => obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number ? p.GetInt64() : null;

    private static decimal? ReadDecimal(JsonElement obj, string name)
    {
        if (obj.ValueKind != JsonValueKind.Object) return null;
        if (!obj.TryGetProperty(name, out var p) || p.ValueKind != JsonValueKind.Number) return null;
        if (p.TryGetDecimal(out var d)) return d;
        return (decimal)p.GetDouble();
    }
}
