using System.Text.Json;

using MediatR;

using Profiqo.Application.Abstractions.Crypto;
using Profiqo.Application.Abstractions.Id;
using Profiqo.Application.Abstractions.Integrations.Trendyol;
using Profiqo.Application.Abstractions.Persistence.Repositories;
using Profiqo.Application.Abstractions.Tenancy;
using Profiqo.Application.Common.Exceptions;
using Profiqo.Domain.Integrations;

namespace Profiqo.Application.Integrations.Trendyol.Commands.ConnectTrendyol;

internal sealed class ConnectTrendyolCommandHandler : IRequestHandler<ConnectTrendyolCommand, Guid>
{
    private readonly ITenantContext _tenant;
    private readonly IProviderConnectionRepository _connections;
    private readonly ISecretProtector _secrets;
    private readonly IIdGenerator _ids;
    private readonly ITrendyolClient _client;

    public ConnectTrendyolCommandHandler(
        ITenantContext tenant,
        IProviderConnectionRepository connections,
        ISecretProtector secrets,
        IIdGenerator ids,
        ITrendyolClient client)
    {
        _tenant = tenant;
        _connections = connections;
        _secrets = secrets;
        _ids = ids;
        _client = client;
    }

    public async Task<Guid> Handle(ConnectTrendyolCommand request, CancellationToken ct)
    {
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null)
            throw new UnauthorizedException("Tenant context missing.");

        var supplierId = (request.SupplierId ?? "").Trim();
        var apiKey = (request.ApiKey ?? "").Trim();
        var apiSecret = (request.ApiSecret ?? "").Trim();
        var displayName = string.IsNullOrWhiteSpace(request.DisplayName) ? "Trendyol" : request.DisplayName.Trim();

        if (string.IsNullOrWhiteSpace(supplierId))
            throw new ArgumentException("SupplierId required.", nameof(request.SupplierId));

        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("ApiKey required.", nameof(request.ApiKey));

        if (string.IsNullOrWhiteSpace(apiSecret))
            throw new ArgumentException("ApiSecret required.", nameof(request.ApiSecret));

        // Quick connectivity test (1 page, narrow window)
        var end = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var start = DateTimeOffset.UtcNow.AddDays(-1).ToUnixTimeMilliseconds();

        _ = await _client.GetOrdersAsync(
            apiKey: apiKey,
            apiSecret: apiSecret,
            supplierId: supplierId,
            page: 0,
            size: 1,
            status: "Created",
            startDateMs: start,
            endDateMs: end,
            ct: ct);

        var credsJson = JsonSerializer.Serialize(new TrendyolCreds(apiKey, apiSecret));
        var enc = _secrets.Protect(credsJson);

        var existing = await _connections.GetByProviderAsync(tenantId.Value, ProviderType.Trendyol, ct);

        if (existing is null)
        {
            var created = ProviderConnection.Create(
                tenantId: tenantId.Value,
                providerType: ProviderType.Trendyol,
                displayName: displayName,
                externalAccountId: supplierId,
                accessToken: enc,
                refreshToken: null,
                accessTokenExpiresAtUtc: null,
                nowUtc: DateTimeOffset.UtcNow);

            await _connections.AddAsync(created, ct);
            return created.Id.Value;
        }

        existing.UpdateProfile(displayName, supplierId, DateTimeOffset.UtcNow);
        existing.RotateTokens(enc, null, null, DateTimeOffset.UtcNow);

        return existing.Id.Value;
    }

    private sealed record TrendyolCreds(string ApiKey, string ApiSecret);
}
