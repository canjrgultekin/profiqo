// Path: backend/src/Profiqo.Application/Integrations/Trendyol/TrendyolSyncProcessor.cs
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
    private const string CursorKey = "trendyol.orders.cursor.lastOrderDateMs";

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
        var connId = new ProviderConnectionId(connectionId);
        var conn = await _connections.GetByIdAsync(connId, ct);
        if (conn is null || conn.TenantId != tenantId || conn.ProviderType != ProviderType.Trendyol)
            throw new InvalidOperationException("Trendyol connection not found for tenant.");

        var sellerId = conn.ExternalAccountId ?? throw new InvalidOperationException("SellerId missing.");
        var credsJson = _secrets.Unprotect(conn.AccessToken);
        var creds = JsonSerializer.Deserialize<TrendyolCreds>(credsJson) ?? throw new InvalidOperationException("Invalid credentials");

        var cursorRaw = await _cursors.GetAsync(tenantId, connId, CursorKey, ct);
        long? cursorMs = long.TryParse(cursorRaw, out var c) && c > 0 ? c : null;

        var endMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var startMsDefault = DateTimeOffset.UtcNow.AddDays(-_opts.BackfillDays).ToUnixTimeMilliseconds();
        var startMs = cursorMs ?? startMsDefault;

        var safeSize = pageSize <= 0 ? _opts.DefaultPageSize : Math.Min(pageSize, _opts.PageSizeMax);

        var processed = 0;
        long maxSeenOrderDate = startMs;

        for (var page = 0; page < maxPages; page++)
        {
            ct.ThrowIfCancellationRequested();

            using var doc = await _client.GetOrdersAsync(
                creds.ApiKey,
                creds.ApiSecret,
                sellerId,
                creds.UserAgent,
                startMs,
                endMs,
                page,
                safeSize,
                _opts.OrderByField,
                ct);

            var root = doc.RootElement;

            if (!root.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array || content.GetArrayLength() == 0)
                break;

            foreach (var o in content.EnumerateArray())
            {
                ct.ThrowIfCancellationRequested();

                var shipmentPackageId = ReadString(o, "shipmentPackageId") ?? ReadString(o, "id") ?? Guid.NewGuid().ToString("N");
                var orderNumber = ReadString(o, "orderNumber") ?? shipmentPackageId;

                var orderDateMs = ReadInt64(o, "orderDate") ?? startMs;
                if (orderDateMs > maxSeenOrderDate) maxSeenOrderDate = orderDateMs;

                var orderDateUtc = DateTimeOffset.FromUnixTimeMilliseconds(orderDateMs);

                var currency = ReadString(o, "currencyCode") ?? "TRY";

                // Trendyol response: totalPrice is net price after discounts (your sample)
                var totalPrice = ReadDecimal(o, "totalPrice") ?? ReadDecimal(o, "packageTotalPrice") ?? 0m;

                var email = ReadString(o, "customerEmail");
                var firstName = ReadString(o, "customerFirstName");
                var lastName = ReadString(o, "customerLastName");

                // phone often null in shipmentAddress.phone; still try it
                string? phone = null;
                if (o.TryGetProperty("shipmentAddress", out var sa) && sa.ValueKind == JsonValueKind.Object)
                    phone = ReadString(sa, "phone");

                var lines = new List<TrendyolOrderLineUpsert>();

                if (o.TryGetProperty("lines", out var li) && li.ValueKind == JsonValueKind.Array)
                {
                    foreach (var l in li.EnumerateArray())
                    {
                        var sku =
                            ReadString(l, "merchantSku") ??
                            ReadString(l, "sku") ??
                            ReadString(l, "barcode") ??
                            ReadString(l, "stockCode") ??
                            "NA";

                        var productName = ReadString(l, "productName") ?? "unknown";
                        var qty = (int)(ReadInt64(l, "quantity") ?? 1);

                        // sample: lineUnitPrice exists and is net per item
                        var unit = ReadDecimal(l, "lineUnitPrice")
                                   ?? ReadDecimal(l, "price")
                                   ?? 0m;

                        var lineCurrency = ReadString(l, "currencyCode") ?? currency;

                        lines.Add(new TrendyolOrderLineUpsert(sku, productName, qty, unit, lineCurrency));
                    }
                }

                var model = new TrendyolOrderUpsert(
                    ShipmentPackageId: shipmentPackageId,
                    OrderNumber: orderNumber,
                    OrderDateUtc: orderDateUtc,
                    CurrencyCode: currency,
                    TotalPrice: totalPrice,
                    CustomerEmail: email,
                    CustomerPhone: phone,
                    CustomerFirstName: firstName,
                    CustomerLastName: lastName,
                    Lines: lines,
                    PayloadJson: o.GetRawText());

                await _store.UpsertOrderAsync(tenantId, model, ct);

                processed++;
                if (processed % 50 == 0)
                    await _jobs.MarkProgressAsync(jobId, processed, ct);
            }

            await _jobs.MarkProgressAsync(jobId, processed, ct);

            // If we got less than requested size, no more pages
            if (content.GetArrayLength() < safeSize)
                break;
        }

        var nextCursor = maxSeenOrderDate > 0 ? (maxSeenOrderDate + 1) : (endMs + 1);
        await _cursors.UpsertAsync(tenantId, connId, CursorKey, nextCursor.ToString(), DateTimeOffset.UtcNow, ct);

        return processed;
    }

    private sealed record TrendyolCreds(string ApiKey, string ApiSecret, string UserAgent);

    private static string? ReadString(JsonElement obj, string name)
        => obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private static long? ReadInt64(JsonElement obj, string name)
        => obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number ? p.GetInt64() : null;

    private static decimal? ReadDecimal(JsonElement obj, string name)
    {
        if (obj.ValueKind != JsonValueKind.Object) return null;
        if (!obj.TryGetProperty(name, out var p)) return null;
        if (p.ValueKind == JsonValueKind.Number && p.TryGetDecimal(out var d)) return d;
        if (p.ValueKind == JsonValueKind.String && decimal.TryParse(p.GetString(), out var x)) return x;
        return null;
    }
}
