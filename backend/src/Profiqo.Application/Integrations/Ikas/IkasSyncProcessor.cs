// Path: backend/src/Profiqo.Application/Integrations/Ikas/IkasSyncProcessor.cs
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Profiqo.Application.Abstractions.Crypto;
using Profiqo.Application.Abstractions.Integrations.Ikas;
using Profiqo.Application.Abstractions.Persistence;
using Profiqo.Application.Abstractions.Persistence.Repositories;
using Profiqo.Domain.Common.Ids;
using Profiqo.Domain.Integrations;

namespace Profiqo.Application.Integrations.Ikas;

public interface IIkasSyncProcessor
{
    Task<int> SyncCustomersAsync(Guid jobId, TenantId tenantId, Guid connectionId, int pageSize, int maxPages, CancellationToken ct);
    Task<int> SyncOrdersAsync(Guid jobId, TenantId tenantId, Guid connectionId, int pageSize, int maxPages, CancellationToken ct);
 //   Task<int> SyncAbandonedCheckoutsAsync(Guid jobId, TenantId tenantId, Guid connectionId, int pageSize, int maxPages, CancellationToken ct);
}

public sealed class IkasSyncProcessor : IIkasSyncProcessor
{
    // İlk entegrasyonda (cursor yokken) Ikas tarafında alabildiğimiz kadar geçmişe dönük veri çekiyoruz.
    // Sonraki sync'lerde cursor ile incremental çalışır.
    private const long InitialCutoffMs = 0;

    private const string CustomerCursorKey = "ikas.customers.cursor.updatedAtMs";
    private const string OrderCursorKey = "ikas.orders.cursor.updatedAtMs";

    private sealed record IkasPrivateAppCreds(string StoreName, string ClientId, string ClientSecret);

    private readonly IProviderConnectionRepository _connections;
    private readonly ISecretProtector _secrets;
    private readonly IIkasGraphqlClient _ikas;
    private readonly IIkasOAuthTokenClient _oauth;
    private readonly IIkasSyncStore _store;
    private readonly IIntegrationJobRepository _jobs;
    private readonly IIntegrationCursorRepository _cursors;

    public IkasSyncProcessor(
        IProviderConnectionRepository connections,
        ISecretProtector secrets,
        IIkasGraphqlClient ikas,
        IIkasOAuthTokenClient oauth,
        IIkasSyncStore store,
        IIntegrationJobRepository jobs,
        IIntegrationCursorRepository cursors)
    {
        _connections = connections;
        _secrets = secrets;
        _ikas = ikas;
        _oauth = oauth;
        _store = store;
        _jobs = jobs;
        _cursors = cursors;
    }

    public async Task<int> SyncCustomersAsync(Guid jobId, TenantId tenantId, Guid connectionId, int pageSize, int maxPages, CancellationToken ct)
    {
        var connId = new ProviderConnectionId(connectionId);
        var conn = await _connections.GetByIdAsync(connId, ct);
        if (conn is null || conn.TenantId != tenantId || conn.ProviderType != ProviderType.Ikas)
            throw new InvalidOperationException("Ikas connection not found for tenant.");

        var (storeName, token) = await GetAuthAsync(conn, ct);

        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var stopBeforeMs = (await GetCursorMsOrNull(tenantId, connId, CustomerCursorKey, ct)) ?? InitialCutoffMs;

        var processed = 0;
        long maxSeenUpdatedAt = stopBeforeMs > 0 ? stopBeforeMs : 0;

        for (var page = 1; page <= maxPages; page++)
        {
            ct.ThrowIfCancellationRequested();

            using var doc = await _ikas.ListCustomersAsync(storeName, token, page, pageSize, ct);

            var list = doc.RootElement.GetProperty("data").GetProperty("listCustomer");
            var data = list.GetProperty("data");

            if (data.ValueKind != JsonValueKind.Array || data.GetArrayLength() == 0)
                break;

            var stopAll = false;

            foreach (var c in data.EnumerateArray())
            {
                ct.ThrowIfCancellationRequested();

                var updatedAtMs = ReadInt64(c, "updatedAt") ?? 0L;

                if (updatedAtMs > 0 && updatedAtMs < stopBeforeMs)
                {
                    stopAll = true;
                    break;
                }

                if (updatedAtMs > maxSeenUpdatedAt)
                    maxSeenUpdatedAt = updatedAtMs;

                var providerCustomerId = ReadString(c, "id") ?? Guid.NewGuid().ToString("N");
                var firstName = ReadString(c, "firstName");
                var lastName = ReadString(c, "lastName");
                var email = ReadString(c, "email");
                var phone = ReadString(c, "phone");

                var emailNorm = NormalizeEmail(email);
                var phoneNorm = NormalizePhone(phone);

                var model = new IkasCustomerUpsert(
                    ProviderCustomerId: providerCustomerId,
                    FirstName: firstName,
                    LastName: lastName,
                    EmailNormalized: emailNorm,
                    EmailHashSha256: string.IsNullOrWhiteSpace(emailNorm) ? null : Sha256Hex(emailNorm),
                    PhoneNormalized: phoneNorm,
                    PhoneHashSha256: string.IsNullOrWhiteSpace(phoneNorm) ? null : Sha256Hex(phoneNorm));

                await _store.UpsertCustomerAsync(tenantId, model, ct);
                processed++;
            }

            await _jobs.MarkProgressAsync(jobId, processed, ct);

            var hasNext = ReadBoolean(list, "hasNext");
            if (!hasNext || stopAll)
                break;
        }

        var nextCursor = maxSeenUpdatedAt > 0 ? maxSeenUpdatedAt : nowMs;
        await _cursors.UpsertAsync(tenantId, connId, CustomerCursorKey, nextCursor.ToString(), DateTimeOffset.UtcNow, ct);

        return processed;
    }

    public async Task<int> SyncOrdersAsync(Guid jobId, TenantId tenantId, Guid connectionId, int pageSize, int maxPages, CancellationToken ct)
    {
        var connId = new ProviderConnectionId(connectionId);
        var conn = await _connections.GetByIdAsync(connId, ct);
        if (conn is null || conn.TenantId != tenantId || conn.ProviderType != ProviderType.Ikas)
            throw new InvalidOperationException("Ikas connection not found for tenant.");

        var (storeName, token) = await GetAuthAsync(conn, ct);

        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var cursorMs = await GetCursorMsOrNull(tenantId, connId, OrderCursorKey, ct);

        // Server-side updatedAt filter sadece cursor varsa kullanılır.
        var updatedAtGteMs = cursorMs;
        var stopBeforeMs = cursorMs ?? InitialCutoffMs;

        var processed = 0;
        long maxSeenUpdatedAt = cursorMs ?? 0;

        for (var page = 1; page <= maxPages; page++)
        {
            ct.ThrowIfCancellationRequested();

            using var doc = await _ikas.ListOrdersAsync(storeName, token, page, pageSize, updatedAtGteMs, ct);

            var list = doc.RootElement.GetProperty("data").GetProperty("listOrder");
            var data = list.GetProperty("data");

            if (data.ValueKind != JsonValueKind.Array || data.GetArrayLength() == 0)
                break;

            var stopAll = false;

            foreach (var o in data.EnumerateArray())
            {
                ct.ThrowIfCancellationRequested();

                var updatedAtMs = ReadInt64(o, "updatedAt") ?? 0L;

                if (updatedAtMs > 0 && updatedAtMs < stopBeforeMs)
                {
                    stopAll = true;
                    break;
                }

                if (updatedAtMs > maxSeenUpdatedAt)
                    maxSeenUpdatedAt = updatedAtMs;

                var orderedAtMs = ReadInt64(o, "orderedAt") ?? 0L;
                var placedAtUtc = orderedAtMs > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(orderedAtMs) : DateTimeOffset.UtcNow;

                var providerOrderId = ReadString(o, "orderNumber");
                if (string.IsNullOrWhiteSpace(providerOrderId))
                    providerOrderId = ReadString(o, "id") ?? Guid.NewGuid().ToString("N");

                var currency = ReadString(o, "currencyCode") ?? "TRY";
                var totalFinal = ReadDecimal(o, "totalFinalPrice") ?? ReadDecimal(o, "totalPrice") ?? 0m;
                var orderStatus = ReadString(o, "status");

                string? emailNorm = null, emailHash = null, phoneNorm = null, phoneHash = null;

                if (o.TryGetProperty("customer", out var cust) && cust.ValueKind == JsonValueKind.Object)
                {
                    var email = ReadString(cust, "email");
                    var phone = ReadString(cust, "phone");

                    emailNorm = NormalizeEmail(email);
                    phoneNorm = NormalizePhone(phone);

                    emailHash = string.IsNullOrWhiteSpace(emailNorm) ? null : Sha256Hex(emailNorm);
                    phoneHash = string.IsNullOrWhiteSpace(phoneNorm) ? null : Sha256Hex(phoneNorm);
                }

                var lines = new List<IkasOrderLineUpsert>();

                if (o.TryGetProperty("orderLineItems", out var oli) && oli.ValueKind == JsonValueKind.Array)
                {
                    foreach (var li in oli.EnumerateArray())
                    {
                        var qty = ReadInt32(li, "quantity") ?? 1;
                        var lineCurrency = ReadString(li, "currencyCode") ?? currency;

                        var unitPrice = ReadDecimal(li, "price") ?? 0m;
                        var finalPrice = ReadDecimal(li, "finalPrice") ?? unitPrice;

                        var lineStatus = ReadString(li, "status");
                        var discount = ReadDecimal(li, "discountPrice") ?? 0m;

                        string? sku = null;
                        string productName = "unknown";
                        string? variantId = null;
                        string? productId = null;

                        string? productCategory = null;
                        string? barcode = null;

                        if (li.TryGetProperty("variant", out var v) && v.ValueKind == JsonValueKind.Object)
                        {
                            variantId = ReadString(v, "id");
                            productId = ReadString(v, "productId");
                            sku = ReadString(v, "sku");
                            productName = ReadString(v, "name") ?? productName;

                            if (v.TryGetProperty("categories", out var cats) && cats.ValueKind == JsonValueKind.Array && cats.GetArrayLength() > 0)
                            {
                                var c0 = cats[0];
                                if (c0.ValueKind == JsonValueKind.Object)
                                    productCategory = ReadString(c0, "name");
                            }

                            if (v.TryGetProperty("barcodeList", out var bl) && bl.ValueKind == JsonValueKind.Array && bl.GetArrayLength() > 0)
                            {
                                var b0 = bl[0];
                                if (b0.ValueKind == JsonValueKind.String)
                                    barcode = b0.GetString();
                            }
                        }

                        lines.Add(new IkasOrderLineUpsert(
                            Sku: sku,
                            ProductName: productName,
                            Quantity: qty,
                            UnitPrice: unitPrice,
                            FinalPrice: finalPrice,
                            CurrencyCode: lineCurrency,
                            ProviderVariantId: variantId,
                            ProviderProductId: productId,
                            ProductCategory: productCategory,
                            Barcode: barcode,
                            Discount: discount,
                            OrderLineItemStatusName: lineStatus));
                    }
                }

                string? shippingJson = null;
                if (o.TryGetProperty("shippingAddress", out var ship) && ship.ValueKind == JsonValueKind.Object)
                    shippingJson = ship.GetRawText();

                string? billingJson = null;
                if (o.TryGetProperty("billingAddress", out var bill) && bill.ValueKind == JsonValueKind.Object)
                    billingJson = bill.GetRawText();

                var model = new IkasOrderUpsert(
                    ProviderOrderId: providerOrderId!,
                    PlacedAtUtc: placedAtUtc,
                    UpdatedAtMs: updatedAtMs,
                    CurrencyCode: currency,
                    TotalFinalPrice: totalFinal,
                    OrderStatus: orderStatus,
                    CustomerEmailNormalized: emailNorm,
                    CustomerEmailHashSha256: emailHash,
                    CustomerPhoneNormalized: phoneNorm,
                    CustomerPhoneHashSha256: phoneHash,
                    Lines: lines,
                    ShippingAddressJson: shippingJson,
                    BillingAddressJson: billingJson);

                await _store.UpsertOrderAsync(tenantId, model, ct);
                processed++;
            }

            await _jobs.MarkProgressAsync(jobId, processed, ct);

            var hasNext = ReadBoolean(list, "hasNext");
            if (!hasNext || stopAll)
                break;
        }

        var nextCursor = maxSeenUpdatedAt > 0 ? maxSeenUpdatedAt : nowMs;
        await _cursors.UpsertAsync(tenantId, connId, OrderCursorKey, nextCursor.ToString(), DateTimeOffset.UtcNow, ct);

        return processed;
    }

    //public async Task<int> SyncAbandonedCheckoutsAsync(Guid jobId, TenantId tenantId, Guid connectionId, int pageSize, int maxPages, CancellationToken ct)
    //{
    //    var connId = new ProviderConnectionId(connectionId);
    //    var conn = await _connections.GetByIdAsync(connId, ct);
    //    if (conn is null || conn.TenantId != tenantId || conn.ProviderType != ProviderType.Ikas)
    //        throw new InvalidOperationException("Ikas connection not found for tenant.");

    //    var (storeName, token) = await GetAuthAsync(conn, ct);

    //    const string cursorKey = "ikas.abandoned.cursor.lastActivityDateMs";

    //    var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    //    var initialCutoffMs = DateTimeOffset.UtcNow.AddDays(-30).ToUnixTimeMilliseconds();

    //    var cursorMs = await GetCursorMsOrNull(tenantId, connId, cursorKey, ct);
    //    var gteMs = cursorMs ?? initialCutoffMs;

    //    var processed = 0;
    //    long maxSeen = cursorMs ?? 0;

    //    for (var page = 1; page <= maxPages; page++)
    //    {
    //        ct.ThrowIfCancellationRequested();

    //        using var doc = await _ikas.ListAbandonedCheckoutsAsync(storeName, token, page, pageSize, gteMs, ct);

    //        var list = doc.RootElement.GetProperty("data").GetProperty("listAbandonedCheckouts");
    //        var data = list.GetProperty("data");

    //        if (data.ValueKind != JsonValueKind.Array || data.GetArrayLength() == 0)
    //            break;

    //        var stopAll = false;

    //        foreach (var a in data.EnumerateArray())
    //        {
    //            ct.ThrowIfCancellationRequested();

    //            var externalId = ReadString(a, "id") ?? Guid.NewGuid().ToString("N");
    //            var status = ReadString(a, "status");

    //            long lastActivity = 0;
    //            string? currency = null;
    //            decimal? totalPrice = null;

    //            if (a.TryGetProperty("cart", out var cart) && cart.ValueKind == JsonValueKind.Object)
    //            {
    //                lastActivity = ReadInt64(cart, "lastActivityDate") ?? 0L;
    //                currency = ReadString(cart, "currencyCode");
    //                totalPrice = ReadDecimal(cart, "totalPrice");
    //            }

    //            if (lastActivity <= 0)
    //            {
    //                var updatedAt = ReadInt64(a, "updatedAt") ?? 0L;
    //                lastActivity = updatedAt > 0 ? updatedAt : nowMs;
    //            }

    //            if (lastActivity > 0 && lastActivity < gteMs)
    //            {
    //                stopAll = true;
    //                break;
    //            }

    //            if (lastActivity > maxSeen) maxSeen = lastActivity;

    //            string? email = null;
    //            string? phone = null;

    //            if (a.TryGetProperty("customer", out var cust) && cust.ValueKind == JsonValueKind.Object)
    //            {
    //                email = NormalizeEmail(ReadString(cust, "email"));
    //                phone = NormalizePhone(ReadString(cust, "phone"));
    //            }

    //            var payloadJson = a.GetRawText();

    //            await _store.UpsertAbandonedCheckoutAsync(
    //                tenantId,
    //                connId,
    //                new IkasAbandonedCheckoutUpsert(
    //                    ExternalId: externalId,
    //                    LastActivityDateMs: lastActivity,
    //                    CurrencyCode: currency,
    //                    TotalFinalPrice: totalPrice,
    //                    Status: status,
    //                    CustomerEmail: string.IsNullOrWhiteSpace(email) ? null : email,
    //                    CustomerPhone: string.IsNullOrWhiteSpace(phone) ? null : phone,
    //                    PayloadJson: payloadJson),
    //                ct);

    //            processed++;

    //            if (processed % 25 == 0)
    //                await _jobs.MarkProgressAsync(jobId, processed, ct);
    //        }

    //        await _jobs.MarkProgressAsync(jobId, processed, ct);

    //        var hasNext = ReadBoolean(list, "hasNext");
    //        if (!hasNext || stopAll) break;
    //    }

    //    var nextCursor = maxSeen > 0 ? (maxSeen + 1) : (nowMs + 1);
    //    await _cursors.UpsertAsync(tenantId, connId, cursorKey, nextCursor.ToString(), DateTimeOffset.UtcNow, ct);

    //    return processed;
    //}

    private async Task<(string StoreName, string AccessToken)> GetAuthAsync(ProviderConnection conn, CancellationToken ct)
    {
        var raw = _secrets.Unprotect(conn.AccessToken);

        var storeFromConn = (conn.ExternalAccountId ?? string.Empty).Trim();

        if (TryParseCreds(raw, out var creds))
        {
            var storeName = string.IsNullOrWhiteSpace(creds.StoreName) ? storeFromConn : creds.StoreName.Trim();
            if (string.IsNullOrWhiteSpace(storeName))
                throw new InvalidOperationException("Ikas storeName missing on connection.");

            var token = await _oauth.GetAccessTokenAsync(storeName, creds.ClientId, creds.ClientSecret, ct);
            return (storeName, token.AccessToken);
        }

        // Legacy token mode
        return (storeFromConn, raw);
    }

    private static bool TryParseCreds(string raw, out IkasPrivateAppCreds creds)
    {
        creds = default!;
        try
        {
            if (string.IsNullOrWhiteSpace(raw)) return false;
            var s = raw.TrimStart();
            if (!s.StartsWith("{", StringComparison.Ordinal)) return false;

            var c = JsonSerializer.Deserialize<IkasPrivateAppCreds>(raw);
            if (c is null) return false;
            if (string.IsNullOrWhiteSpace(c.ClientId) || string.IsNullOrWhiteSpace(c.ClientSecret)) return false;

            creds = c;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<long?> GetCursorMsOrNull(TenantId tenantId, ProviderConnectionId connId, string key, CancellationToken ct)
    {
        var s = await _cursors.GetAsync(tenantId, connId, key, ct);
        if (long.TryParse(s, out var ms) && ms > 0)
            return ms;
        return null;
    }

    private static string NormalizeEmail(string? email)
        => (email ?? string.Empty).Trim().ToLowerInvariant();

    private static string NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return string.Empty;

        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("90")) return "+" + digits;
        if (digits.Length == 10) return "+90" + digits;
        return digits.Length > 0 ? "+" + digits : string.Empty;
    }

    private static string Sha256Hex(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }

    private static string? ReadString(JsonElement obj, string name)
        => obj.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private static long? ReadInt64(JsonElement obj, string name)
        => obj.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number ? p.GetInt64() : null;

    private static int? ReadInt32(JsonElement obj, string name)
        => obj.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number ? p.GetInt32() : null;

    private static bool ReadBoolean(JsonElement obj, string name)
        => obj.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.True;

    private static decimal? ReadDecimal(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var p) || p.ValueKind != JsonValueKind.Number) return null;
        if (p.TryGetDecimal(out var d)) return d;
        return (decimal)p.GetDouble();
    }
}
