using MediatR;

using Profiqo.Application.Abstractions.Crypto;
using Profiqo.Application.Abstractions.Id;
using Profiqo.Application.Abstractions.Persistence.Repositories;
using Profiqo.Application.Abstractions.Tenancy;
using Profiqo.Application.Common.Exceptions;
using Profiqo.Domain.Common.Ids;
using Profiqo.Domain.Integrations;

namespace Profiqo.Application.Integrations.Commands.UpsertProviderConnection;

internal sealed class UpsertProviderConnectionCommandHandler : IRequestHandler<UpsertProviderConnectionCommand, Guid>
{
    private readonly ITenantContext _tenantContext;
    private readonly IProviderConnectionRepository _connections;
    private readonly IIdGenerator _ids;
    private readonly ISecretProtector _secrets;

    public UpsertProviderConnectionCommandHandler(
        ITenantContext tenantContext,
        IProviderConnectionRepository connections,
        IIdGenerator ids,
        ISecretProtector secrets)
    {
        _tenantContext = tenantContext;
        _connections = connections;
        _ids = ids;
        _secrets = secrets;
    }

    public async Task<Guid> Handle(UpsertProviderConnectionCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.CurrentTenantId;
        if (tenantId is null)
            throw new UnauthorizedException("Tenant context missing.");

        var existing = await _connections.GetByProviderAsync(tenantId.Value, request.ProviderType, cancellationToken);

        var access = _secrets.Protect(request.AccessToken);
        var refresh = string.IsNullOrWhiteSpace(request.RefreshToken) ? null : _secrets.Protect(request.RefreshToken!);
        var now = DateTimeOffset.UtcNow;

        if (existing is null)
        {

            var created = ProviderConnection.Create(
                tenantId: tenantId.Value,
                providerType: request.ProviderType,
                displayName: request.DisplayName.Trim(),
                externalAccountId: request.ExternalAccountId?.Trim(),
                accessToken: access,
                refreshToken: refresh,
                accessTokenExpiresAtUtc: request.AccessTokenExpiresAtUtc,
                nowUtc: now);

            await _connections.AddAsync(created, cancellationToken);
            return created.Id.Value;
        }


        existing.UpdateProfile(
            displayName: request.DisplayName.Trim(),
            externalAccountId: request.ExternalAccountId?.Trim(),
            nowUtc: now);

        existing.RotateTokens(
            accessToken: access,
            refreshToken: refresh,
            accessTokenExpiresAtUtc: request.AccessTokenExpiresAtUtc,
            nowUtc: now);


        return existing.Id.Value;
    }
}
