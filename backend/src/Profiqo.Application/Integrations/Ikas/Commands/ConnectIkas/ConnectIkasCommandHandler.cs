using System.Text.Json;

using MediatR;

using Microsoft.EntityFrameworkCore;

using Profiqo.Application.Abstractions.Crypto;
using Profiqo.Application.Abstractions.Integrations.Ikas;
using Profiqo.Application.Abstractions.Persistence.Repositories;
using Profiqo.Application.Abstractions.Tenancy;
using Profiqo.Application.Common.Exceptions;
using Profiqo.Domain.Integrations;

namespace Profiqo.Application.Integrations.Ikas.Commands.ConnectIkas;

internal sealed class ConnectIkasCommandHandler : IRequestHandler<ConnectIkasCommand, Guid>
{
    private sealed record IkasPrivateAppCreds(string StoreName, string ClientId, string ClientSecret);

    private readonly ITenantContext _tenant;
    private readonly IProviderConnectionRepository _connections;
    private readonly ISecretProtector _secrets;
    private readonly IIkasGraphqlClient _ikas;
    private readonly IIkasOAuthTokenClient _oauth;

    public ConnectIkasCommandHandler(
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

    public async Task<Guid> Handle(ConnectIkasCommand request, CancellationToken ct)
    {
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null)
            throw new UnauthorizedException("Tenant context missing.");

        var label = (request.StoreLabel ?? string.Empty).Trim();
        var storeName = (request.StoreName ?? string.Empty).Trim();
        var clientId = (request.ClientId ?? string.Empty).Trim();
        var clientSecret = (request.ClientSecret ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(label))
            throw new AppValidationException(new Dictionary<string, string[]>
            {
                ["storeLabel"] = new[] { "StoreLabel required." }
            });

        if (string.IsNullOrWhiteSpace(storeName))
            throw new AppValidationException(new Dictionary<string, string[]>
            {
                ["storeName"] = new[] { "StoreName required." }
            });

        var now = DateTimeOffset.UtcNow;

        for (var attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                var existing = await _connections.GetByProviderAsync(tenantId.Value, ProviderType.Ikas, ct);

                if (existing is null)
                {
                    if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
                        throw new AppValidationException(new Dictionary<string, string[]>
                        {
                            ["clientId"] = new[] { "ClientId required." },
                            ["clientSecret"] = new[] { "ClientSecret required." }
                        });

                    var token = await _oauth.GetAccessTokenAsync(storeName, clientId, clientSecret, ct);
                    _ = await _ikas.MeAsync(storeName, token.AccessToken, ct);

                    var credsJson = JsonSerializer.Serialize(new IkasPrivateAppCreds(storeName, clientId, clientSecret));
                    var encCreds = _secrets.Protect(credsJson);

                    var created = ProviderConnection.Create(
                        tenantId: tenantId.Value,
                        providerType: ProviderType.Ikas,
                        displayName: label,
                        externalAccountId: storeName,
                        accessToken: encCreds,
                        refreshToken: null,
                        accessTokenExpiresAtUtc: null,
                        nowUtc: now);

                    await _connections.AddAsync(created, ct);
                    return created.Id.Value;
                }

                existing.UpdateProfile(label, storeName, now);

                if (!string.IsNullOrWhiteSpace(clientId) && !string.IsNullOrWhiteSpace(clientSecret))
                {
                    var token = await _oauth.GetAccessTokenAsync(storeName, clientId, clientSecret, ct);
                    _ = await _ikas.MeAsync(storeName, token.AccessToken, ct);

                    var credsJson = JsonSerializer.Serialize(new IkasPrivateAppCreds(storeName, clientId, clientSecret));
                    var encCreds = _secrets.Protect(credsJson);

                    existing.RotateTokens(encCreds, refreshToken: null, accessTokenExpiresAtUtc: null, nowUtc: now);
                }

                return existing.Id.Value;
            }
            catch (DbUpdateConcurrencyException) when (attempt == 1)
            {
                await _connections.ClearTrackingAsync(ct);
            }
        }

        throw new ConflictException("concurrency_conflict = Connection was updated concurrently. Please retry.");
    }
}
