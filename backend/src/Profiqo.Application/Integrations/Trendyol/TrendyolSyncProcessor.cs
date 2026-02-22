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
    private readonly IProviderConnectionRepository _connections;
    private readonly ISecretProtector _secrets;
    private readonly ITrendyolClient _client;
    private readonly ITrendyolSyncStore _store;
    private readonly IIntegrationJobRepository _jobs;
    private readonly TrendyolOptions _opts;

    public TrendyolSyncProcessor(
        IProviderConnectionRepository connections,
        ISecretProtector secrets,
        ITrendyolClient client,
        ITrendyolSyncStore store,
        IIntegrationJobRepository jobs,
        IOptions<TrendyolOptions> opts)
    {
        _connections = connections;
        _secrets = secrets;
        _client = client;
        _store = store;
        _jobs = jobs;
        _opts = opts.Value;
    }

    //public async Task<int> SyncOrdersAsync(Guid jobId, TenantId tenantId, Guid connectionId, int pageSize, int maxPages, CancellationToken ct)
    //{
    //    var connId = new ProviderConnectionId(connectionId);
    //    var conn = await _connections.GetByIdAsync(connId, ct);
    //    if (conn is null || conn.TenantId != tenantId || conn.ProviderType != ProviderType.Trendyol)
    //        throw new InvalidOperationException("Trendyol connection not found for tenant.");

    //    var sellerId = conn.ExternalAccountId ?? throw new InvalidOperationException("SellerId missing.");
    //    var credsJson = _secrets.Unprotect(conn.AccessToken);
    //    var creds = JsonSerializer.Deserialize<TrendyolCreds>(credsJson) ?? throw new InvalidOperationException("Trendyol credentials invalid.");

    //    //var cursorRaw = await _cursors.GetAsync(tenantId, connId, CursorKey, ct);
    //    //long? cursorMs = long.TryParse(cursorRaw, out var c) && c > 0 ? c : null;
    //    var endDate = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    //    var startDate = DateTimeOffset.UtcNow.AddDays(-60).ToUnixTimeMilliseconds();
    //    var endMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    //    var startMsDefault = DateTimeOffset.UtcNow.AddDays(-_opts.BackfillDays).ToUnixTimeMilliseconds();
    //    var startMs = cursorMs ?? startMsDefault;

    //    // Cursor çok eskiyse Trendyol API 3 ay sınırından dolayı clamp
    //    if (startMs < startMsDefault) startMs = startMsDefault;

    //    var safeSize = pageSize <= 0 ? _opts.DefaultPageSize : Math.Min(pageSize, _opts.PageSizeMax);
    //    var safeMaxPages = maxPages <= 0 ? _opts.DefaultMaxPages : maxPages;

    //    var processed = 0;

    //    for (var page = 0; page < safeMaxPages; page++)
    //    {
    //        ct.ThrowIfCancellationRequested();

    //        using var doc = await _client.GetOrdersAsync(
    //            apiKey: creds.ApiKey,
    //            apiSecret: creds.ApiSecret,
    //            sellerId: sellerId,
    //            userAgent: creds.UserAgent,
    //            startDateMs: startMs,
    //            endDateMs: endMs,
    //            page: page,
    //            size: safeSize,
    //            orderByField: _opts.OrderByField,
    //            ct: ct);

    //        var root = doc.RootElement;

    //        if (!root.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array || content.GetArrayLength() == 0)
    //            break;

    //        foreach (var o in content.EnumerateArray())
    //        {
    //            ct.ThrowIfCancellationRequested();

    //            var shipmentPackageId =
    //                ReadStringOrNumber(o, "shipmentPackageId") ??
    //                ReadStringOrNumber(o, "id") ??
    //                ReadStringOrNumber(o, "orderNumber");

    //            var orderNumber =
    //                ReadStringOrNumber(o, "orderNumber") ??
    //                shipmentPackageId ??
    //                "NA";

    //            var orderStatus =
    //                ReadString(o, "status") ??
    //                ReadString(o, "shipmentPackageStatus") ??
    //                ReadString(o, "shipmentPackageStatusName");

    //            var orderDateMs = ReadInt64(o, "orderDate") ?? startMs;
    //            var orderDateUtc = DateTimeOffset.FromUnixTimeMilliseconds(orderDateMs);

    //            var currency = ReadString(o, "currencyCode") ?? "TRY";
    //            var totalPrice = ReadDecimal(o, "totalPrice") ?? ReadDecimal(o, "packageTotalPrice") ?? 0m;

    //            var email = ReadString(o, "customerEmail");
    //            var firstName = ReadString(o, "customerFirstName");
    //            var lastName = ReadString(o, "customerLastName");

    //            string? phone = null;
    //            TrendyolAddressDto? shipAddr = null;
    //            if (o.TryGetProperty("shipmentAddress", out var ship) && ship.ValueKind == JsonValueKind.Object)
    //            {
    //                phone = ReadString(ship, "phone");
    //                shipAddr = ParseAddress(ship);
    //            }

    //            TrendyolAddressDto? billAddr = null;
    //            if (o.TryGetProperty("invoiceAddress", out var inv) && inv.ValueKind == JsonValueKind.Object)
    //                billAddr = ParseAddress(inv);

    //            var lines = new List<TrendyolOrderLineUpsert>();
    //            if (o.TryGetProperty("lines", out var li) && li.ValueKind == JsonValueKind.Array)
    //            {
    //                foreach (var l in li.EnumerateArray())
    //                {
    //                    var barcode = ReadString(l, "barcode");

    //                    var sku =
    //                        ReadString(l, "merchantSku") ??
    //                        ReadString(l, "sku") ??
    //                        barcode ??
    //                        ReadString(l, "stockCode") ??
    //                        "NA";

    //                    var productName = ReadString(l, "productName") ?? "unknown";
    //                    var qty = (int)(ReadInt64(l, "quantity") ?? 1);

    //                    var unit = ReadDecimal(l, "lineUnitPrice") ?? ReadDecimal(l, "price") ?? 0m;
    //                    var lineCurrency = ReadString(l, "currencyCode") ?? currency;

    //                    var productCategoryId =
    //                        ReadStringOrNumber(l, "productCategoryId") ??
    //                        ReadStringOrNumber(l, "productCategory");

    //                    var discount = ReadDecimal(l, "discount") ?? 0m;

    //                    var lineStatusName =
    //                        ReadString(l, "orderLineItemStatusName") ??
    //                        ReadString(l, "orderLineItemStatus") ??
    //                        ReadString(l, "status");

    //                    lines.Add(new TrendyolOrderLineUpsert(
    //                        Sku: sku,
    //                        ProductName: productName,
    //                        Quantity: qty,
    //                        UnitPrice: unit,
    //                        CurrencyCode: lineCurrency,
    //                        ProductCategoryId: productCategoryId,
    //                        Barcode: barcode,
    //                        Discount: discount,
    //                        OrderLineItemStatusName: lineStatusName));
    //                }
    //            }

    //            var model = new TrendyolOrderUpsert(
    //                ShipmentPackageId: shipmentPackageId,
    //                OrderNumber: orderNumber,
    //                OrderDateUtc: orderDateUtc,
    //                CurrencyCode: currency,
    //                TotalPrice: totalPrice,
    //                OrderStatus: orderStatus,
    //                CustomerEmail: email,
    //                CustomerPhone: phone,
    //                CustomerFirstName: firstName,
    //                CustomerLastName: lastName,
    //                Lines: lines,
    //                PayloadJson: o.GetRawText(),
    //                ShippingAddress: shipAddr,
    //                BillingAddress: billAddr);

    //            await _store.UpsertOrderAsync(tenantId, model, ct);

    //            processed++;
    //            if (processed % 25 == 0)
    //                await _jobs.MarkProgressAsync(jobId, processed, ct);
    //        }

    //        await _jobs.MarkProgressAsync(jobId, processed, ct);

    //        if (content.GetArrayLength() < safeSize)
    //            break;
    //    }

    //    return processed;
    //}
    
    public async Task<int> SyncOrdersAsync(Guid jobId, TenantId tenantId, Guid connectionId, int pageSize, int maxPages, CancellationToken ct)
    {
        var connId = new ProviderConnectionId(connectionId);
        var conn = await _connections.GetByIdAsync(connId, ct);
        if (conn is null || conn.TenantId != tenantId || conn.ProviderType != ProviderType.Trendyol)
            throw new InvalidOperationException("Trendyol connection not found for tenant.");

        var sellerId = conn.ExternalAccountId ?? throw new InvalidOperationException("SellerId missing.");
        var credsJson = _secrets.Unprotect(conn.AccessToken);
        var creds = JsonSerializer.Deserialize<TrendyolCreds>(credsJson) ?? throw new InvalidOperationException("Trendyol credentials invalid.");

        // Cursor yok. Her koşuda: bugünden geriye 60 gün window.
        // Trendyol limit 3 ay, 60 gün zaten güvenli.
        var (startMs, endMs) = ComputeIstanbulWindowMs(daysBack: 60);

        var safeSize = pageSize <= 0 ? _opts.DefaultPageSize : Math.Min(pageSize, _opts.PageSizeMax);
        var safeMaxPages = maxPages <= 0 ? _opts.DefaultMaxPages : maxPages;

        var processed = 0;

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

                // Provider key deterministik olsun diye: shipmentPackageId -> id -> orderNumber sırası.
                // (Store tarafında da providerOrderId zaten shipmentPackageId’den gidiyor.)
                var shipmentPackageId =
                    ReadStringOrNumber(o, "shipmentPackageId") ??
                    ReadStringOrNumber(o, "id") ??
                    ReadStringOrNumber(o, "orderNumber") ??
                    Guid.NewGuid().ToString("N");

                var orderNumber =
                    ReadStringOrNumber(o, "orderNumber") ??
                    shipmentPackageId;

                var orderStatus =
                    ReadString(o, "status") ??
                    ReadString(o, "shipmentPackageStatus") ??
                    ReadString(o, "shipmentPackageStatusName");

                var orderDateMs = ReadInt64(o, "orderDate") ?? startMs;
                var orderDateUtc = DateTimeOffset.FromUnixTimeMilliseconds(orderDateMs);

                var currency = ReadString(o, "currencyCode") ?? "TRY";
                var totalPrice = ReadDecimal(o, "totalPrice") ?? ReadDecimal(o, "packageTotalPrice") ?? 0m;

                var email = ReadString(o, "customerEmail");
                var firstName = ReadString(o, "customerFirstName");
                var lastName = ReadString(o, "customerLastName");

                string? phone = null;
                TrendyolAddressDto? shipAddr = null;
                if (o.TryGetProperty("shipmentAddress", out var ship) && ship.ValueKind == JsonValueKind.Object)
                {
                    phone = ReadString(ship, "phone");
                    shipAddr = ParseAddress(ship);
                }

                TrendyolAddressDto? billAddr = null;
                if (o.TryGetProperty("invoiceAddress", out var inv) && inv.ValueKind == JsonValueKind.Object)
                    billAddr = ParseAddress(inv);

                var lines = new List<TrendyolOrderLineUpsert>();
                if (o.TryGetProperty("lines", out var li) && li.ValueKind == JsonValueKind.Array)
                {
                    foreach (var l in li.EnumerateArray())
                    {
                        var barcode = ReadString(l, "barcode");

                        var sku =
                            ReadString(l, "merchantSku") ??
                            ReadString(l, "sku") ??
                            barcode ??
                            ReadString(l, "stockCode") ??
                            "NA";

                        var productName = ReadString(l, "productName") ?? "unknown";
                        var qty = (int)(ReadInt64(l, "quantity") ?? 1);

                        var unit = ReadDecimal(l, "lineUnitPrice") ?? ReadDecimal(l, "price") ?? 0m;
                        var lineCurrency = ReadString(l, "currencyCode") ?? currency;

                        var productCategoryId =
                            ReadStringOrNumber(l, "productCategoryId") ??
                            ReadStringOrNumber(l, "productCategory");

                        var discount = ReadDecimal(l, "discount") ?? 0m;

                        var lineStatusName =
                            ReadString(l, "orderLineItemStatusName") ??
                            ReadString(l, "orderLineItemStatus") ??
                            ReadString(l, "status");

                        lines.Add(new TrendyolOrderLineUpsert(
                            Sku: sku,
                            ProductName: productName,
                            Quantity: qty,
                            UnitPrice: unit,
                            CurrencyCode: lineCurrency,
                            ProductCategoryId: productCategoryId,
                            Barcode: barcode,
                            Discount: discount,
                            OrderLineItemStatusName: lineStatusName));
                    }
                }

                var model = new TrendyolOrderUpsert(
                    ShipmentPackageId: shipmentPackageId,
                    OrderNumber: orderNumber,
                    OrderDateUtc: orderDateUtc,
                    CurrencyCode: currency,
                    TotalPrice: totalPrice,
                    OrderStatus: orderStatus,
                    CustomerEmail: email,
                    CustomerPhone: phone,
                    CustomerFirstName: firstName,
                    CustomerLastName: lastName,
                    Lines: lines,
                    PayloadJson: o.GetRawText(),
                    ShippingAddress: shipAddr,
                    BillingAddress: billAddr);

                // Aynı providerOrderId tekrar gelirse store idempotent davranır, DB’ye mükerrer yazmaz.
                await _store.UpsertOrderAsync(tenantId, model, ct);

                processed++;
                if (processed % 25 == 0)
                    await _jobs.MarkProgressAsync(jobId, processed, ct);
            }

            await _jobs.MarkProgressAsync(jobId, processed, ct);

            if (content.GetArrayLength() < safeSize)
                break;
        }

        return processed;
    }

    private sealed record TrendyolCreds(string ApiKey, string ApiSecret, string UserAgent);

    private static (long startMs, long endMs) ComputeIstanbulWindowMs(int daysBack)
    {
        var tz = GetIstanbulTimeZone();
        var nowTr = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz);

        // Başlangıç: 60 gün önce Istanbul 00:00:00
        var startTr = new DateTimeOffset(nowTr.Date, nowTr.Offset).AddDays(-daysBack);

        var startMs = startTr.ToUnixTimeMilliseconds();
        var endMs = nowTr.ToUnixTimeMilliseconds();

        if (endMs <= startMs)
            endMs = startMs + 1;

        return (startMs, endMs);
    }

    private static TimeZoneInfo GetIstanbulTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul"); } // Linux
        catch
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time"); } // Windows
            catch { return TimeZoneInfo.Utc; }
        }
    }

    private static TrendyolAddressDto ParseAddress(JsonElement a)
    {
        var address1 = ReadString(a, "address1");
        var address2 = ReadString(a, "address2");
        var city = ReadString(a, "city");
        var cityCode = (int?)ReadInt64(a, "cityCode");
        var district = ReadString(a, "district");
        var districtId = (int?)ReadInt64(a, "districtId");
        var countryCode = ReadString(a, "countryCode");
        var postalCode = ReadString(a, "postalCode");
        var phone = ReadString(a, "phone");
        var fullName = ReadString(a, "fullName");

        return new TrendyolAddressDto(
            Address1: address1,
            Address2: address2,
            City: city,
            CityCode: cityCode,
            District: district,
            DistrictId: districtId,
            CountryCode: countryCode,
            PostalCode: postalCode,
            Phone: phone,
            FullName: fullName);
    }

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