using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Profiqo.Application.Abstractions.Crypto;
using Profiqo.Application.Abstractions.Integrations.Ikas;
using Profiqo.Application.Abstractions.Persistence;
using Profiqo.Application.Abstractions.Persistence.Repositories;
using Profiqo.Domain.Common.Ids;
using Profiqo.Domain.Integrations;

using static System.Reflection.Metadata.BlobBuilder;

namespace Profiqo.Application.Integrations.Ikas;

public interface IIkasSyncProcessor
{
    Task<int> SyncCustomersAsync(Guid jobId, TenantId tenantId, Guid connectionId, int pageSize, int maxPages, CancellationToken ct);
    Task<int> SyncOrdersAsync(Guid jobId, TenantId tenantId, Guid connectionId, int pageSize, int maxPages, CancellationToken ct);
    Task<int> SyncAbandonedCheckoutsAsync(Guid jobId, TenantId tenantId, Guid connectionId, int pageSize, int maxPages, CancellationToken ct);

}

public sealed class IkasSyncProcessor : IIkasSyncProcessor
{
    private const int InitialBackfillDays = 30;

    // Cursor keys (ms since epoch)
    private const string CustomerCursorKey = "ikas.customers.cursor.updatedAtMs";
    private const string OrderCursorKey = "ikas.orders.cursor.orderedAtMs";

    private readonly IProviderConnectionRepository _connections;
    private readonly ISecretProtector _secrets;
    private readonly IIkasGraphqlClient _ikas;
    private readonly IIkasSyncStore _store;
    private readonly IIntegrationJobRepository _jobs;
    private readonly IIntegrationCursorRepository _cursors;

    public IkasSyncProcessor(
        IProviderConnectionRepository connections,
        ISecretProtector secrets,
        IIkasGraphqlClient ikas,
        IIkasSyncStore store,
        IIntegrationJobRepository jobs,
        IIntegrationCursorRepository cursors)
    {
        _connections = connections;
        _secrets = secrets;
        _ikas = ikas;
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

        var token = _secrets.Unprotect(conn.AccessToken);

        // Cursor is "last processed updatedAt" (ms)
        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var initialCutoffMs = DateTimeOffset.UtcNow.AddDays(-InitialBackfillDays).ToUnixTimeMilliseconds();

        var cursorMs = await GetCursorMsOrNull(tenantId, connId, CustomerCursorKey, ct);
        var stopBeforeMs = cursorMs ?? initialCutoffMs; // stop when item.updatedAt < stopBeforeMs

        var processed = 0;
        long maxSeenUpdatedAt = cursorMs ?? 0;

        // Server-side updatedAt filter is NOT used (you confirmed it makes result empty).
        // We rely on sorting (-updatedAt) and stop condition on client side.
        for (var page = 1; page <= maxPages; page++)
        {
            ct.ThrowIfCancellationRequested();

            using var doc = await _ikas.ListCustomersAsync(token, page, pageSize, ct);

            var list = doc.RootElement.GetProperty("data").GetProperty("listCustomer");
            var data = list.GetProperty("data");

            if (data.ValueKind != JsonValueKind.Array || data.GetArrayLength() == 0)
                break;

            var stopAll = false;

            foreach (var c in data.EnumerateArray())
            {
                ct.ThrowIfCancellationRequested();

                var updatedAtMs = ReadInt64(c, "updatedAt") ?? 0L;

                // Because sort is -updatedAt, once we cross the threshold we can stop entire paging
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

            // Optional: break if API says no next page
            var hasNext = ReadBoolean(list, "hasNext");
            if (!hasNext || stopAll)
                break;
        }

        // Cursor advance: avoid reprocessing exact same timestamp
        // If cursor was null (first sync), we set it to maxSeenUpdatedAt; if no updatedAt available, fallback to now.
        var nextCursor = maxSeenUpdatedAt > 0 ? (maxSeenUpdatedAt + 1) : (nowMs + 1);
        await _cursors.UpsertAsync(tenantId, connId, CustomerCursorKey, nextCursor.ToString(), DateTimeOffset.UtcNow, ct);

        return processed;
    }

    public async Task<int> SyncOrdersAsync(Guid jobId, TenantId tenantId, Guid connectionId, int pageSize, int maxPages, CancellationToken ct)
    {
        var connId = new ProviderConnectionId(connectionId);
        var conn = await _connections.GetByIdAsync(connId, ct);
        if (conn is null || conn.TenantId != tenantId || conn.ProviderType != ProviderType.Ikas)
            throw new InvalidOperationException("Ikas connection not found for tenant.");

        var token = _secrets.Unprotect(conn.AccessToken);

        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var initialCutoffMs = DateTimeOffset.UtcNow.AddDays(-InitialBackfillDays).ToUnixTimeMilliseconds();

        var cursorMs = await GetCursorMsOrNull(tenantId, connId, OrderCursorKey, ct);
        var orderedAtGteMs = cursorMs ?? initialCutoffMs; // server-side orderedAt filter (works per your curl)

        var processed = 0;
        long maxSeenOrderedAt = cursorMs ?? 0;

        for (var page = 1; page <= maxPages; page++)
        {
            ct.ThrowIfCancellationRequested();

            using var doc = await _ikas.ListOrdersAsync(token, page, pageSize, orderedAtGteMs, ct);

            var list = doc.RootElement.GetProperty("data").GetProperty("listOrder");
            var data = list.GetProperty("data");

            if (data.ValueKind != JsonValueKind.Array || data.GetArrayLength() == 0)
                break;

            var stopAll = false;

            foreach (var o in data.EnumerateArray())
            {
                ct.ThrowIfCancellationRequested();

                var orderedAtMs = ReadInt64(o, "orderedAt") ?? 0L;
                if (orderedAtMs > 0 && orderedAtMs < orderedAtGteMs)
                {
                    // sorted -orderedAt, safe to stop
                    stopAll = true;
                    break;
                }

                if (orderedAtMs > maxSeenOrderedAt)
                    maxSeenOrderedAt = orderedAtMs;

                var providerOrderId = ReadString(o, "orderNumber");
                if (string.IsNullOrWhiteSpace(providerOrderId))
                    providerOrderId = ReadString(o, "id") ?? Guid.NewGuid().ToString("N");

                var currency = ReadString(o, "currencyCode") ?? "TRY";
                var totalFinal = ReadDecimal(o, "totalFinalPrice") ?? 0m;

                var placedAtUtc = orderedAtMs > 0
                    ? DateTimeOffset.FromUnixTimeMilliseconds(orderedAtMs)
                    : DateTimeOffset.UtcNow;

                // Customer identities from order.customer
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

                // ✅ Order line mapping from orderLineItems
                var lines = new List<IkasOrderLineUpsert>();

                if (o.TryGetProperty("orderLineItems", out var oli) && oli.ValueKind == JsonValueKind.Array)
                {
                    foreach (var li in oli.EnumerateArray())
                    {
                        var qty = ReadInt32(li, "quantity") ?? 1;
                        var lineCurrency = ReadString(li, "currencyCode") ?? currency;

                        var unitPrice = ReadDecimal(li, "price") ?? 0m;
                        var finalPrice = ReadDecimal(li, "finalPrice") ?? unitPrice;

                        string? sku = null;
                        string productName = "unknown";
                        string? variantId = null;
                        string? productId = null;

                        if (li.TryGetProperty("variant", out var v) && v.ValueKind == JsonValueKind.Object)
                        {
                            variantId = ReadString(v, "id");
                            productId = ReadString(v, "productId");
                            sku = ReadString(v, "sku");
                            productName = ReadString(v, "name") ?? productName;
                        }

                        lines.Add(new IkasOrderLineUpsert(
                            Sku: sku,
                            ProductName: productName,
                            Quantity: qty,
                            UnitPrice: unitPrice,
                            FinalPrice: finalPrice,
                            CurrencyCode: lineCurrency,
                            ProviderVariantId: variantId,
                            ProviderProductId: productId));
                    }
                }

                var model = new IkasOrderUpsert(
                    ProviderOrderId: providerOrderId!,
                    PlacedAtUtc: placedAtUtc,
                    UpdatedAtMs: ReadInt64(o, "updatedAt") ?? 0L,
                    CurrencyCode: currency,
                    TotalFinalPrice: totalFinal,
                    CustomerEmailNormalized: emailNorm,
                    CustomerEmailHashSha256: emailHash,
                    CustomerPhoneNormalized: phoneNorm,
                    CustomerPhoneHashSha256: phoneHash,
                    Lines: lines);

                await _store.UpsertOrderAsync(tenantId, model, ct);
                processed++;
            }

            await _jobs.MarkProgressAsync(jobId, processed, ct);

            var hasNext = ReadBoolean(list, "hasNext");
            if (!hasNext || stopAll)
                break;
        }

        // Cursor advance (orderedAt-based)
        var nextCursor = maxSeenOrderedAt > 0 ? (maxSeenOrderedAt + 1) : (nowMs + 1);
        await _cursors.UpsertAsync(tenantId, connId, OrderCursorKey, nextCursor.ToString(), DateTimeOffset.UtcNow, ct);

        return processed;
    }

  

public async Task<int> SyncAbandonedCheckoutsAsync(
    Guid jobId,
    TenantId tenantId,
    Guid connectionId,
    int pageSize,
    int maxPages,
    CancellationToken ct)
{
    var connId = new ProviderConnectionId(connectionId);
    var conn = await _connections.GetByIdAsync(connId, ct);
    if (conn is null || conn.TenantId != tenantId || conn.ProviderType != ProviderType.Ikas)
        throw new InvalidOperationException("Ikas connection not found for tenant.");

    var token = _secrets.Unprotect(conn.AccessToken);

    const string cursorKey = "ikas.abandoned.cursor.lastActivityDateMs";

    var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    var initialCutoffMs = DateTimeOffset.UtcNow.AddDays(-30).ToUnixTimeMilliseconds();

    var cursorMs = await GetCursorMsOrNull(tenantId, connId, cursorKey, ct);
    var gteMs = cursorMs ?? initialCutoffMs;

    var processed = 0;
    long maxSeen = cursorMs ?? 0;

    for (var page = 1; page <= maxPages; page++)
    {
        ct.ThrowIfCancellationRequested();

        using var doc = await _ikas.ListAbandonedCheckoutsAsync(token, page, pageSize, gteMs, ct);

        var list = doc.RootElement.GetProperty("data").GetProperty("listAbandonedCheckouts");
        var data = list.GetProperty("data");

        if (data.ValueKind != JsonValueKind.Array || data.GetArrayLength() == 0)
            break;

        var stopAll = false;

        foreach (var a in data.EnumerateArray())
        {
            ct.ThrowIfCancellationRequested();

            var externalId = ReadString(a, "id") ?? Guid.NewGuid().ToString("N");
            var status = ReadString(a, "status");

            long lastActivity = 0;
            string? currency = null;
            decimal? totalPrice = null;

            if (a.TryGetProperty("cart", out var cart) && cart.ValueKind == JsonValueKind.Object)
            {
                lastActivity = ReadInt64(cart, "lastActivityDate") ?? 0L;
                currency = ReadString(cart, "currencyCode");
                totalPrice = ReadDecimal(cart, "totalPrice"); // ✅ FIX
            }

            if (lastActivity <= 0)
            {
                var updatedAt = ReadInt64(a, "updatedAt") ?? 0L;
                lastActivity = updatedAt > 0 ? updatedAt : nowMs;
            }

            if (lastActivity > 0 && lastActivity < gteMs)
            {
                stopAll = true;
                break;
            }

            if (lastActivity > maxSeen) maxSeen = lastActivity;

            string? email = null;
            string? phone = null;

            if (a.TryGetProperty("customer", out var cust) && cust.ValueKind == JsonValueKind.Object)
            {
                email = NormalizeEmail(ReadString(cust, "email"));
                phone = NormalizePhone(ReadString(cust, "phone"));
            }

            var payloadJson = a.GetRawText();

            await _store.UpsertAbandonedCheckoutAsync(
                tenantId,
                connId,
                new IkasAbandonedCheckoutUpsert(
                    ExternalId: externalId,
                    LastActivityDateMs: lastActivity,
                    CurrencyCode: currency,
                    TotalFinalPrice: totalPrice, // we store it in TotalFinalPrice field for now (name is generic)
                    Status: status,
                    CustomerEmail: string.IsNullOrWhiteSpace(email) ? null : email,
                    CustomerPhone: string.IsNullOrWhiteSpace(phone) ? null : phone,
                    PayloadJson: payloadJson),
                ct);

            processed++;

            if (processed % 25 == 0)
                await _jobs.MarkProgressAsync(jobId, processed, ct);
        }

        await _jobs.MarkProgressAsync(jobId, processed, ct);

        var hasNext = ReadBoolean(list, "hasNext");
        if (!hasNext || stopAll) break;
    }

    var nextCursor = maxSeen > 0 ? (maxSeen + 1) : (nowMs + 1);
    await _cursors.UpsertAsync(tenantId, connId, cursorKey, nextCursor.ToString(), DateTimeOffset.UtcNow, ct);

    return processed;
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
