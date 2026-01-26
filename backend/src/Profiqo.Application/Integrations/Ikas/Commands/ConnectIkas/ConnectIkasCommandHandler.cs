using MediatR;

using Profiqo.Application.Abstractions.Crypto;
using Profiqo.Application.Abstractions.Integrations.Ikas;
using Profiqo.Application.Abstractions.Persistence.Repositories;
using Profiqo.Application.Abstractions.Tenancy;
using Profiqo.Application.Common.Exceptions;
using Profiqo.Domain.Integrations;

namespace Profiqo.Application.Integrations.Ikas.Commands.ConnectIkas;

internal sealed class ConnectIkasCommandHandler : IRequestHandler<ConnectIkasCommand, Guid>
{
    private readonly ITenantContext _tenant;
    private readonly IProviderConnectionRepository _connections;
    private readonly ISecretProtector _secrets;
    private readonly IIkasGraphqlClient _ikas;

    public ConnectIkasCommandHandler(
        ITenantContext tenant,
        IProviderConnectionRepository connections,
        ISecretProtector secrets,
        IIkasGraphqlClient ikas)
    {
        _tenant = tenant;
        _connections = connections;
        _secrets = secrets;
        _ikas = ikas;
    }

    public async Task<Guid> Handle(ConnectIkasCommand request, CancellationToken ct)
    {
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null)
            throw new UnauthorizedException("Tenant context missing.");

        var label = request.StoreLabel?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(label))
            throw new AppValidationException(new Dictionary<string, string[]>
            {
                ["storeLabel"] = new[] { "StoreLabel required." }
            });

        var token = request.AccessToken?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(token))
            throw new AppValidationException(new Dictionary<string, string[]>
            {
                ["accessToken"] = new[] { "AccessToken required." }
            });

        // ✅ Validate token BEFORE persisting
        _ = await _ikas.MeAsync(token, ct);

        var existing = await _connections.GetByProviderAsync(tenantId.Value, ProviderType.Ikas, ct);

        var enc = _secrets.Protect(token);
        var now = DateTimeOffset.UtcNow;

        if (existing is null)
        {
            var created = ProviderConnection.Create(
                tenantId: tenantId.Value,
                providerType: ProviderType.Ikas,
                displayName: label,
                externalAccountId: request.StoreDomain?.Trim(),
                accessToken: enc,
                refreshToken: null,
                accessTokenExpiresAtUtc: null,
                nowUtc: now);

            await _connections.AddAsync(created, ct);
            return created.Id.Value;
        }

        existing.UpdateProfile(label, request.StoreDomain?.Trim(), now);
        existing.RotateTokens(enc, null, null, now);

        return existing.Id.Value;
    }
}
