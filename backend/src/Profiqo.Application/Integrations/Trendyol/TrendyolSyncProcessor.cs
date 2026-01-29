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

        // Cursor: last seen orderDateMs
        var cursorRaw = await _cursors.GetAsync(tenantId, connId, CursorKey, ct);
        long? cursorMs = long.TryParse(cursorRaw, out var c) && c > 0 ? c : null;

        var endMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var startMsDefault = DateTimeOffset.UtcNow.AddDays(-_opts.BackfillDays).ToUnixTimeMilliseconds();
        var startMs = cursorMs ?? startMsDefault;

        var safeSize = pageSize <= 0 ? _opts.DefaultPageSize : Math.Min(pageSize, _opts.PageSizeMax);
        var safeMaxPages = maxPages <= 0 ? _opts.DefaultMaxPages : maxPages;

        var processed = 0;
        long maxSeenOrderDate = startMs;

        for (var page = 0; page < safeMaxPages; page++)
        {
            ct.ThrowIfCancellationRequested();

            using var doc = await _client.GetOrdersAsync(
                apiKey: creds.ApiKey,
                apiSecret: creds.ApiSecret,
                sellerId: sellerId,
                userAgent: creds.UserAgent,
                startDateMs: startMs,
                endDateMs: endMs,
                page: page,
                size: safeSize,
                orderByField: _opts.OrderByField,
                ct: ct);

            var root = doc.RootElement;

            if (!root.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array || content.GetArrayLength() == 0)
                break;

            foreach (var o in content.EnumerateArray())
            {
                ct.ThrowIfCancellationRequested();

                // shipmentPackageId is NUMBER in real payload -> read as string safely
                var shipmentPackageId =
                    ReadStringOrNumber(o, "shipmentPackageId") ??
                    ReadStringOrNumber(o, "id") ??
                    Guid.NewGuid().ToString("N");

                var orderNumber =
                    ReadStringOrNumber(o, "orderNumber") ??
                    shipmentPackageId;

                var orderDateMs = ReadInt64(o, "orderDate") ?? startMs;
                if (orderDateMs > maxSeenOrderDate) maxSeenOrderDate = orderDateMs;

                var orderDateUtc = DateTimeOffset.FromUnixTimeMilliseconds(orderDateMs);

                var currency = ReadString(o, "currencyCode") ?? "TRY";

                // totalPrice and packageTotalPrice are numbers
                var totalPrice = ReadDecimal(o, "totalPrice") ?? ReadDecimal(o, "packageTotalPrice") ?? 0m;

                var email = ReadString(o, "customerEmail");
                var firstName = ReadString(o, "customerFirstName");
                var lastName = ReadString(o, "customerLastName");

                string? phone = null;
                if (o.TryGetProperty("shipmentAddress", out var shipAddr) && shipAddr.ValueKind == JsonValueKind.Object)
                    phone = ReadString(shipAddr, "phone");

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

                        // lineUnitPrice is NUMBER in real payload
                        var unit = ReadDecimal(l, "lineUnitPrice") ?? ReadDecimal(l, "price") ?? 0m;

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
                if (processed % 25 == 0)
                    await _jobs.MarkProgressAsync(jobId, processed, ct);
            }

            await _jobs.MarkProgressAsync(jobId, processed, ct);

            if (content.GetArrayLength() < safeSize)
                break;
        }

        // advance cursor
        var nextCursor = maxSeenOrderDate > 0 ? (maxSeenOrderDate + 1) : (endMs + 1);
        await _cursors.UpsertAsync(tenantId, connId, CursorKey, nextCursor.ToString(), DateTimeOffset.UtcNow, ct);

        return processed;
    }

    private sealed record TrendyolCreds(string ApiKey, string ApiSecret, string UserAgent);

    private static string? ReadString(JsonElement obj, string name)
        => obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private static string? ReadStringOrNumber(JsonElement obj, string name)
    {
        if (obj.ValueKind != JsonValueKind.Object) return null;
        if (!obj.TryGetProperty(name, out var p)) return null;

        return p.ValueKind switch
        {
            JsonValueKind.String => p.GetString(),
            JsonValueKind.Number => p.TryGetInt64(out var l) ? l.ToString() : p.GetDouble().ToString(System.Globalization.CultureInfo.InvariantCulture),
            _ => null
        };
    }

    private static long? ReadInt64(JsonElement obj, string name)
        => obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number ? p.GetInt64() : null;

    private static decimal? ReadDecimal(JsonElement obj, string name)
    {
        if (obj.ValueKind != JsonValueKind.Object) return null;
        if (!obj.TryGetProperty(name, out var p)) return null;

        if (p.ValueKind == JsonValueKind.Number && p.TryGetDecimal(out var d)) return d;
        if (p.ValueKind == JsonValueKind.String && decimal.TryParse(p.GetString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var x)) return x;
        return null;
    }
}
