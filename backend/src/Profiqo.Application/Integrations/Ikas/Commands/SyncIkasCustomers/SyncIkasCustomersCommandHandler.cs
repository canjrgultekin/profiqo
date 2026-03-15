using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using MediatR;

using Profiqo.Application.Abstractions.Crypto;
using Profiqo.Application.Abstractions.Integrations.Ikas;
using Profiqo.Application.Abstractions.Persistence.Repositories;
using Profiqo.Application.Abstractions.Tenancy;
using Profiqo.Application.Common.Exceptions;
using Profiqo.Domain.Customers;
using Profiqo.Domain.Integrations;

namespace Profiqo.Application.Integrations.Ikas.Commands.SyncIkasCustomers;

internal sealed class SyncIkasCustomersCommandHandler : IRequestHandler<SyncIkasCustomersCommand, int>
{
    private readonly ITenantContext _tenant;
    private readonly IProviderConnectionRepository _connections;
    private readonly ISecretProtector _secrets;
    private readonly IIkasGraphqlClient _ikas;
    private readonly IIkasSyncStore _store;

    public SyncIkasCustomersCommandHandler(
        ITenantContext tenant,
        IProviderConnectionRepository connections,
        ISecretProtector secrets,
        IIkasGraphqlClient ikas,
        IIkasSyncStore store)
    {
        _tenant = tenant;
        _connections = connections;
        _secrets = secrets;
        _ikas = ikas;
        _store = store;
    }

    public async Task<int> Handle(SyncIkasCustomersCommand request, CancellationToken ct)
    {
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null)
            throw new UnauthorizedException("Tenant context missing.");

        var conn = await _connections.GetByIdAsync(new Profiqo.Domain.Common.Ids.ProviderConnectionId(request.ConnectionId), ct);
        if (conn is null || conn.ProviderType != ProviderType.Ikas || conn.TenantId != tenantId.Value)
            throw new NotFoundException("Ikas connection not found.");

        var token = _secrets.Unprotect(conn.AccessToken);

        var limit = request.PageSize is null or < 1 or > 200 ? 50 : request.PageSize.Value;
        var maxPages = request.MaxPages is null or < 1 or > 500 ? 20 : request.MaxPages.Value;

        var processed = 0;

        for (var page = 1; page <= maxPages; page++)
        {
            using var doc = await _ikas.ListCustomersAsync(token, page, limit, ct);

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

                await _store.UpsertCustomerAsync(tenantId.Value, model, ct);
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
