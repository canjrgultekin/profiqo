using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Profiqo.Application.Abstractions.Crypto;
using Profiqo.Application.Abstractions.Integrations.Ikas;
using Profiqo.Application.Abstractions.Persistence.Repositories;
using Profiqo.Domain.Common.Ids;
using Profiqo.Domain.Integrations;

namespace Profiqo.Application.Integrations.Ikas;

public interface IIkasSyncProcessor
{
    Task<int> SyncCustomersAsync(TenantId tenantId, Guid connectionId, int pageSize, int maxPages, CancellationToken ct);
    Task<int> SyncOrdersAsync(TenantId tenantId, Guid connectionId, int pageSize, int maxPages, CancellationToken ct);
}

public sealed class IkasSyncProcessor : IIkasSyncProcessor
{
    private readonly IProviderConnectionRepository _connections;
    private readonly ISecretProtector _secrets;
    private readonly IIkasGraphqlClient _ikas;
    private readonly IIkasSyncStore _store;

    public IkasSyncProcessor(
        IProviderConnectionRepository connections,
        ISecretProtector secrets,
        IIkasGraphqlClient ikas,
        IIkasSyncStore store)
    {
        _connections = connections;
        _secrets = secrets;
        _ikas = ikas;
        _store = store;
    }

    public async Task<int> SyncCustomersAsync(TenantId tenantId, Guid connectionId, int pageSize, int maxPages, CancellationToken ct)
    {
        var conn = await _connections.GetByIdAsync(new ProviderConnectionId(connectionId), ct);
        if (conn is null || conn.TenantId != tenantId || conn.ProviderType != ProviderType.Ikas)
            throw new InvalidOperationException("Ikas connection not found for tenant.");

        var token = _secrets.Unprotect(conn.AccessToken);

        var processed = 0;

        for (var page = 1; page <= maxPages; page++)
        {
            using var doc = await _ikas.ListCustomersAsync(token, page, pageSize, ct);

            var arr = doc.RootElement.GetProperty("data").GetProperty("listCustomer").GetProperty("data");
            if (arr.ValueKind != JsonValueKind.Array || arr.GetArrayLength() == 0)
                break;

            foreach (var c in arr.EnumerateArray())
            {
                var providerCustomerId = c.GetProperty("id").GetString() ?? Guid.NewGuid().ToString("N");

                var firstName = c.TryGetProperty("firstName", out var fn) ? fn.GetString() : null;
                var lastName = c.TryGetProperty("lastName", out var ln) ? ln.GetString() : null;

                var email = c.TryGetProperty("email", out var em) ? em.GetString() : null;
                var phone = c.TryGetProperty("phone", out var ph) ? ph.GetString() : null;

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
        }

        return processed;
    }

    public async Task<int> SyncOrdersAsync(TenantId tenantId, Guid connectionId, int pageSize, int maxPages, CancellationToken ct)
    {
        var conn = await _connections.GetByIdAsync(new ProviderConnectionId(connectionId), ct);
        if (conn is null || conn.TenantId != tenantId || conn.ProviderType != ProviderType.Ikas)
            throw new InvalidOperationException("Ikas connection not found for tenant.");

        var token = _secrets.Unprotect(conn.AccessToken);

        var processed = 0;

        for (var page = 1; page <= maxPages; page++)
        {
            using var doc = await _ikas.ListOrdersAsync(token, page, pageSize, ct);

            var arr = doc.RootElement.GetProperty("data").GetProperty("listOrder").GetProperty("data");
            if (arr.ValueKind != JsonValueKind.Array || arr.GetArrayLength() == 0)
                break;

            foreach (var o in arr.EnumerateArray())
            {
                var providerOrderId =
                    o.TryGetProperty("orderNumber", out var on) ? (on.GetString() ?? "") : "";

                if (string.IsNullOrWhiteSpace(providerOrderId))
                    providerOrderId = o.GetProperty("id").GetString() ?? Guid.NewGuid().ToString("N");

                var currency = o.TryGetProperty("currencyCode", out var cc) ? (cc.GetString() ?? "TRY") : "TRY";
                var totalFinal = o.TryGetProperty("totalFinalPrice", out var tf) ? (decimal)tf.GetDouble() : 0m;

                var placedAt = DateTimeOffset.UtcNow;
                if (o.TryGetProperty("orderedAt", out var oa) && oa.ValueKind == JsonValueKind.Number)
                {
                    var ms = oa.GetInt64();
                    if (ms > 0) placedAt = DateTimeOffset.FromUnixTimeMilliseconds(ms);
                }

                string? emailNorm = null, emailHash = null, phoneNorm = null, phoneHash = null;

                if (o.TryGetProperty("customer", out var cust) && cust.ValueKind == JsonValueKind.Object)
                {
                    var email = cust.TryGetProperty("email", out var em) ? em.GetString() : null;
                    var phone = cust.TryGetProperty("phone", out var ph) ? ph.GetString() : null;

                    emailNorm = NormalizeEmail(email);
                    phoneNorm = NormalizePhone(phone);

                    emailHash = string.IsNullOrWhiteSpace(emailNorm) ? null : Sha256Hex(emailNorm);
                    phoneHash = string.IsNullOrWhiteSpace(phoneNorm) ? null : Sha256Hex(phoneNorm);
                }

                var model = new IkasOrderUpsert(
                    ProviderOrderId: providerOrderId,
                    PlacedAtUtc: placedAt,
                    CurrencyCode: currency,
                    TotalFinalPrice: totalFinal,
                    CustomerEmailNormalized: emailNorm,
                    CustomerEmailHashSha256: emailHash,
                    CustomerPhoneNormalized: phoneNorm,
                    CustomerPhoneHashSha256: phoneHash);

                await _store.UpsertOrderAsync(tenantId, model, ct);
                processed++;
            }
        }

        return processed;
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
}
