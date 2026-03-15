// Path: backend/src/Profiqo.Application/Integrations/Hepsiburada/HepsiburadaSyncProcessor.cs
using System.Text.Json;

using Microsoft.Extensions.Options;

using Profiqo.Application.Abstractions.Crypto;
using Profiqo.Application.Abstractions.Integrations.Hepsiburada;
using Profiqo.Application.Abstractions.Persistence;
using Profiqo.Application.Abstractions.Persistence.Repositories;
using Profiqo.Domain.Common.Ids;
using Profiqo.Domain.Integrations;

namespace Profiqo.Application.Integrations.Hepsiburada;

public sealed class HepsiburadaSyncProcessor : IHepsiburadaSyncProcessor
{
    private readonly IProviderConnectionRepository _connections;
    private readonly ISecretProtector _secrets;
    private readonly IHepsiburadaClient _client;
    private readonly IHepsiburadaSyncStore _store;
    private readonly IIntegrationJobRepository _jobs;
    private readonly HepsiburadaOptions _opts;

    public HepsiburadaSyncProcessor(
        IProviderConnectionRepository connections,
        ISecretProtector secrets,
        IHepsiburadaClient client,
        IHepsiburadaSyncStore store,
        IIntegrationJobRepository jobs,
        IOptions<HepsiburadaOptions> opts)
    {
        _connections = connections;
        _secrets = secrets;
        _client = client;
        _store = store;
        _jobs = jobs;
        _opts = opts.Value;
    }

    public async Task<int> SyncOrdersAsync(Guid jobId, TenantId tenantId, Guid connectionId, int pageSize, int maxPages, CancellationToken ct)
    {
        var connId = new ProviderConnectionId(connectionId);
        var conn = await _connections.GetByIdAsync(connId, ct);
        if (conn is null || conn.TenantId != tenantId || conn.ProviderType != ProviderType.Hepsiburada)
            throw new InvalidOperationException("Hepsiburada connection not found for tenant.");

        var merchantId = conn.ExternalAccountId ?? throw new InvalidOperationException("MerchantId missing.");
        var credsJson = _secrets.Unprotect(conn.AccessToken);
        var creds = JsonSerializer.Deserialize<HepsiburadaCreds>(credsJson) ?? throw new InvalidOperationException("Hepsiburada credentials invalid.");

        // Istanbul timezone ile tarih penceresi hesapla
        var (beginDate, endDate) = ComputeIstanbulWindow(daysBack: _opts.BackfillDays);

        var safeLimit = pageSize <= 0 ? _opts.DefaultLimit : Math.Min(pageSize, _opts.LimitMax);
        var safeMaxPages = maxPages <= 0 ? _opts.DefaultMaxPages : maxPages;

        var processed = 0;

        // HB offset/limit ile pagination: her sayfada offset = page * limit
        for (var page = 0; page < safeMaxPages; page++)
        {
            ct.ThrowIfCancellationRequested();

            var offset = page * safeLimit;

            using var doc = await _client.GetPaidOrdersAsync(
                username: creds.Username,
                password: creds.Password,
                merchantId: merchantId,
                offset: offset,
                limit: safeLimit,
                beginDate: beginDate,
                endDate: endDate,
                ct: ct);

            var root = doc.RootElement;

            // HB response: { totalCount, items: [...] } veya doğrudan array
            JsonElement items;
            if (root.TryGetProperty("items", out var itemsProp) && itemsProp.ValueKind == JsonValueKind.Array)
                items = itemsProp;
            else if (root.ValueKind == JsonValueKind.Array)
                items = root;
            else
                break;

            if (items.GetArrayLength() == 0)
                break;

            // HB flat line item döner, orderNumber bazında grupla
            var orderGroups = new Dictionary<string, List<JsonElement>>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in items.EnumerateArray())
            {
                var orderNumber = ReadString(item, "orderNumber") ?? "NA";
                if (!orderGroups.TryGetValue(orderNumber, out var list))
                {
                    list = new List<JsonElement>();
                    orderGroups[orderNumber] = list;
                }
                list.Add(item.Clone());
            }

            foreach (var (orderNumber, lineItems) in orderGroups)
            {
                ct.ThrowIfCancellationRequested();

                var firstItem = lineItems[0];

                var orderDateStr = ReadString(firstItem, "orderDate");
                var orderDateUtc = TryParseDate(orderDateStr);

                var orderStatus = ReadString(firstItem, "status");
                var customerName = ReadString(firstItem, "customerName");

                // Müşteri email ve phone shippingAddress veya invoice.address'ten gelir
                string? customerEmail = null;
                string? customerPhone = null;
                HepsiburadaAddressDto? shipAddr = null;
                HepsiburadaAddressDto? billAddr = null;

                if (firstItem.TryGetProperty("shippingAddress", out var ship) && ship.ValueKind == JsonValueKind.Object)
                {
                    customerEmail = ReadString(ship, "email");
                    customerPhone = ReadString(ship, "phoneNumber") ?? ReadString(ship, "alternatePhoneNumber");
                    shipAddr = ParseAddress(ship);
                }

                if (firstItem.TryGetProperty("invoice", out var inv) && inv.ValueKind == JsonValueKind.Object)
                {
                    if (inv.TryGetProperty("address", out var invAddr) && invAddr.ValueKind == JsonValueKind.Object)
                    {
                        billAddr = ParseAddress(invAddr);
                        if (string.IsNullOrWhiteSpace(customerEmail))
                            customerEmail = ReadString(invAddr, "email");
                        if (string.IsNullOrWhiteSpace(customerPhone))
                            customerPhone = ReadString(invAddr, "phoneNumber");
                    }
                }

                // HB customerId alanı (opsiyonel)
                var hbCustomerId = ReadString(firstItem, "customerId");

                // Order toplam fiyat: tüm line item totalPrice'ların toplamı
                decimal totalPrice = 0m;
                var currency = "TRY";

                var lines = new List<HepsiburadaOrderLineUpsert>();
                foreach (var l in lineItems)
                {
                    var sku = ReadString(l, "sku") ?? "NA";
                    var merchantSku = ReadString(l, "merchantSku");
                    var productName = ReadString(l, "productName") ?? ReadString(l, "name") ?? "unknown";
                    var qty = (int)(ReadInt64(l, "quantity") ?? 1);

                    decimal unitPrice = 0m;
                    var unitCurrency = "TRY";
                    if (l.TryGetProperty("unitPrice", out var up) && up.ValueKind == JsonValueKind.Object)
                    {
                        unitPrice = ReadDecimal(up, "amount") ?? 0m;
                        unitCurrency = ReadString(up, "currency") ?? "TRY";
                    }
                    else
                    {
                        unitPrice = ReadDecimal(l, "unitPrice") ?? 0m;
                    }

                    decimal lineTotalPrice = 0m;
                    if (l.TryGetProperty("totalPrice", out var tp) && tp.ValueKind == JsonValueKind.Object)
                    {
                        lineTotalPrice = ReadDecimal(tp, "amount") ?? 0m;
                        currency = ReadString(tp, "currency") ?? "TRY";
                    }
                    else
                    {
                        lineTotalPrice = ReadDecimal(l, "totalPrice") ?? 0m;
                    }

                    totalPrice += lineTotalPrice;

                    var vat = ReadDecimal(l, "vat") ?? 0m;
                    var vatRate = ReadDecimal(l, "vatRate");

                    // HB'de discount: unitMerchantDiscount.amount veya totalMerchantDiscount.amount
                    decimal discount = 0m;
                    if (l.TryGetProperty("unitMerchantDiscount", out var umd) && umd.ValueKind == JsonValueKind.Object)
                        discount = ReadDecimal(umd, "amount") ?? 0m;
                    else if (l.TryGetProperty("totalMerchantDiscount", out var tmd) && tmd.ValueKind == JsonValueKind.Object)
                        discount = ReadDecimal(tmd, "amount") ?? 0m;

                    var lineStatusName = ReadString(l, "status");

                    lines.Add(new HepsiburadaOrderLineUpsert(
                        Sku: sku,
                        MerchantSku: merchantSku,
                        ProductName: productName,
                        Quantity: qty,
                        UnitPrice: unitPrice,
                        CurrencyCode: unitCurrency,
                        Vat: vat,
                        VatRate: vatRate,
                        Discount: discount,
                        TotalPrice: lineTotalPrice,
                        OrderLineItemStatusName: lineStatusName));
                }

                // Payload: tüm line item'ların JSON'ını array olarak sakla
                var payloadJson = JsonSerializer.Serialize(lineItems.Select(x => JsonSerializer.Deserialize<object>(x.GetRawText())));

                var model = new HepsiburadaOrderUpsert(
                    OrderNumber: orderNumber,
                    OrderDateUtc: orderDateUtc,
                    CurrencyCode: currency,
                    TotalPrice: totalPrice,
                    OrderStatus: orderStatus,
                    CustomerName: customerName,
                    CustomerEmail: customerEmail,
                    CustomerPhone: customerPhone,
                    CustomerId: hbCustomerId,
                    Lines: lines,
                    PayloadJson: payloadJson,
                    ShippingAddress: shipAddr,
                    BillingAddress: billAddr);

                await _store.UpsertOrderAsync(tenantId, model, ct);

                processed++;
                if (processed % 25 == 0)
                    await _jobs.MarkProgressAsync(jobId, processed, ct);
            }

            await _jobs.MarkProgressAsync(jobId, processed, ct);

            // Son sayfa: dönen kayıt sayısı limit'ten azsa bitir
            if (items.GetArrayLength() < safeLimit)
                break;

            // HB totalCount ile de kontrol: root.totalCount varsa toplam kayıtla karşılaştır
            if (root.TryGetProperty("totalCount", out var tc) && tc.ValueKind == JsonValueKind.Number)
            {
                var total = tc.GetInt32();
                if (offset + items.GetArrayLength() >= total)
                    break;
            }
        }

        return processed;
    }

    private sealed record HepsiburadaCreds(string Username, string Password);

    private static (string beginDate, string endDate) ComputeIstanbulWindow(int daysBack)
    {
        var tz = GetIstanbulTimeZone();
        var nowTr = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz);

        var startTr = new DateTimeOffset(nowTr.Date, nowTr.Offset).AddDays(-daysBack);

        // HB format: yyyy-MM-dd HH:mm
        var beginDate = startTr.ToString("yyyy-MM-dd HH:mm");
        var endDate = nowTr.ToString("yyyy-MM-dd HH:mm");

        return (beginDate, endDate);
    }

    private static TimeZoneInfo GetIstanbulTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul"); }
        catch
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time"); }
            catch { return TimeZoneInfo.Utc; }
        }
    }

    private static DateTimeOffset TryParseDate(string? dateStr)
    {
        if (string.IsNullOrWhiteSpace(dateStr))
            return DateTimeOffset.UtcNow;

        if (DateTimeOffset.TryParse(dateStr, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var dto))
            return dto;

        if (DateTime.TryParse(dateStr, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var dt))
            return new DateTimeOffset(dt, TimeSpan.Zero);

        return DateTimeOffset.UtcNow;
    }

    private static HepsiburadaAddressDto ParseAddress(JsonElement a)
    {
        var addressDetail = ReadString(a, "addressDetail") ?? ReadString(a, "address");
        var city = ReadString(a, "city");
        var town = ReadString(a, "town");
        var district = ReadString(a, "district");
        var countryCode = ReadString(a, "countryCode");
        var postalCode = ReadString(a, "postalCode");
        var phone = ReadString(a, "phoneNumber") ?? ReadString(a, "alternatePhoneNumber");
        var email = ReadString(a, "email");
        var fullName = ReadString(a, "name") ?? ReadString(a, "recipientName") ?? ReadString(a, "fullName");

        return new HepsiburadaAddressDto(
            AddressDetail: addressDetail,
            City: city,
            Town: town,
            District: district,
            CountryCode: countryCode,
            PostalCode: postalCode,
            Phone: phone,
            Email: email,
            FullName: fullName);
    }

    private static string? ReadString(JsonElement obj, string name)
        => obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

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