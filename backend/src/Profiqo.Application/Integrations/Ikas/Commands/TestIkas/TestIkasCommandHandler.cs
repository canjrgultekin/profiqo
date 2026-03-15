using System.Text.Json;

using MediatR;

using Profiqo.Application.Abstractions.Crypto;
using Profiqo.Application.Abstractions.Integrations.Ikas;
using Profiqo.Application.Abstractions.Persistence.Repositories;
using Profiqo.Application.Abstractions.Tenancy;
using Profiqo.Application.Common.Exceptions;
using Profiqo.Domain.Common.Ids;
using Profiqo.Domain.Integrations;

namespace Profiqo.Application.Integrations.Ikas.Commands.TestIkas;

internal sealed class TestIkasCommandHandler : IRequestHandler<TestIkasCommand, string>
{
    private sealed record IkasPrivateAppCreds(string StoreName, string ClientId, string ClientSecret);

    private readonly ITenantContext _tenant;
    private readonly IProviderConnectionRepository _connections;
    private readonly ISecretProtector _secrets;
    private readonly IIkasGraphqlClient _ikas;
    private readonly IIkasOAuthTokenClient _oauth;

    public TestIkasCommandHandler(
        ITenantContext tenant,
        IProviderConnectionRepository connections,
        ISecretProtector secrets,
        IIkasGraphqlClient ikas,
        IIkasOAuthTokenClient oauth)
    {
        _tenant = tenant;
        _connections = connections;
        _secrets = secrets;
        _ikas = ikas;
        _oauth = oauth;
    }

    public async Task<string> Handle(TestIkasCommand request, CancellationToken ct)
    {
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null)
            throw new UnauthorizedException("Tenant context missing.");

        var conn = await _connections.GetByIdAsync(new ProviderConnectionId(request.ConnectionId), ct);
        if (conn is null || conn.ProviderType != ProviderType.Ikas || conn.TenantId != tenantId.Value)
            throw new NotFoundException("Ikas connection not found.");

        var storeName = (conn.ExternalAccountId ?? string.Empty).Trim();
        var raw = _secrets.Unprotect(conn.AccessToken);

        if (TryParseCreds(raw, out var creds))
        {
            storeName = string.IsNullOrWhiteSpace(creds.StoreName) ? storeName : creds.StoreName.Trim();
            var token = await _oauth.GetAccessTokenAsync(storeName, creds.ClientId, creds.ClientSecret, ct);
            return await _ikas.MeAsync(storeName, token.AccessToken, ct);
        }

        return await _ikas.MeAsync(storeName, raw, ct);
    }

    private static bool TryParseCreds(string raw, out IkasPrivateAppCreds creds)
    {
        creds = default!;
        try
        {
            if (string.IsNullOrWhiteSpace(raw)) return false;
            if (!raw.TrimStart().StartsWith("{", StringComparison.Ordinal)) return false;
            var c = JsonSerializer.Deserialize<IkasPrivateAppCreds>(raw);
            if (c is null) return false;
            if (string.IsNullOrWhiteSpace(c.StoreName) || string.IsNullOrWhiteSpace(c.ClientId) || string.IsNullOrWhiteSpace(c.ClientSecret))
                return false;
            creds = c;
            return true;
        }
        catch
        {
            return false;
        }
    }
}
