// Path: backend/src/Profiqo.Application/Integrations/Trendyol/Commands/ConnectTrendyol/ConnectTrendyolCommandHandler.cs
using System.Text.Json;

using MediatR;

using Microsoft.Extensions.Options;

using Profiqo.Application.Abstractions.Crypto;
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
    private readonly ITrendyolClient _client;
    private readonly TrendyolOptions _opts;

    public ConnectTrendyolCommandHandler(
        ITenantContext tenant,
        IProviderConnectionRepository connections,
        ISecretProtector secrets,
        ITrendyolClient client,
        IOptions<TrendyolOptions> opts)
    {
        _tenant = tenant;
        _connections = connections;
        _secrets = secrets;
        _client = client;
        _opts = opts.Value;
    }

    public async Task<Guid> Handle(ConnectTrendyolCommand request, CancellationToken ct)
    {
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null)
            throw new UnauthorizedException("Tenant context missing.");

        var sellerId = (request.SellerId ?? "").Trim();
        var apiKey = (request.ApiKey ?? "").Trim();
        var apiSecret = (request.ApiSecret ?? "").Trim();
        var displayName = string.IsNullOrWhiteSpace(request.DisplayName) ? "Trendyol" : request.DisplayName.Trim();
        var userAgent = string.IsNullOrWhiteSpace(request.UserAgent) ? $"Profiqo/{sellerId}" : request.UserAgent!.Trim();

        if (string.IsNullOrWhiteSpace(sellerId))
            throw new ArgumentException("SellerId required.", nameof(request.SellerId));

        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("ApiKey required.", nameof(request.ApiKey));

        if (string.IsNullOrWhiteSpace(apiSecret))
            throw new ArgumentException("ApiSecret required.", nameof(request.ApiSecret));

        // Validate connectivity: last 1 day, 1 item
        var endMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var startMs = DateTimeOffset.UtcNow.AddDays(-1).ToUnixTimeMilliseconds();

        _ = await _client.GetOrdersAsync(
            apiKey: apiKey,
            apiSecret: apiSecret,
            sellerId: sellerId,
            userAgent: userAgent,
            startDateMs: startMs,
            endDateMs: endMs,
            page: 0,
            size: 1,
            orderByField: _opts.OrderByField,
            ct: ct);

        // Store encrypted JSON { apiKey, apiSecret, userAgent }
        var credsJson = JsonSerializer.Serialize(new TrendyolCreds(apiKey, apiSecret, userAgent));
        var enc = _secrets.Protect(credsJson);

        var existing = await _connections.GetByProviderAsync(tenantId.Value, ProviderType.Trendyol, ct);

        if (existing is null)
        {
            var created = ProviderConnection.Create(
                tenantId: tenantId.Value,
                providerType: ProviderType.Trendyol,
                displayName: displayName,
                externalAccountId: sellerId,
                accessToken: enc,
                refreshToken: null,
                accessTokenExpiresAtUtc: null,
                nowUtc: DateTimeOffset.UtcNow);

            await _connections.AddAsync(created, ct);

            // ✅ UnitOfWorkBehavior şimdi bunu command olarak görecek ve SaveChanges çalışacak
            return created.Id.Value;
        }

        existing.UpdateProfile(displayName, sellerId, DateTimeOffset.UtcNow);
        existing.RotateTokens(enc, null, null, DateTimeOffset.UtcNow);

        return existing.Id.Value;
    }

    private sealed record TrendyolCreds(string ApiKey, string ApiSecret, string UserAgent);
}
