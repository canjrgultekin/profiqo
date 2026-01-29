// Path: backend/src/Profiqo.Application/Integrations/Trendyol/Commands/TestTrendyol/TestTrendyolCommandHandler.cs
using System.Text.Json;

using MediatR;

using Microsoft.Extensions.Options;

using Profiqo.Application.Abstractions.Crypto;
using Profiqo.Application.Abstractions.Integrations.Trendyol;
using Profiqo.Application.Abstractions.Persistence.Repositories;
using Profiqo.Application.Abstractions.Tenancy;
using Profiqo.Application.Common.Exceptions;
using Profiqo.Domain.Common.Ids;
using Profiqo.Domain.Integrations;

namespace Profiqo.Application.Integrations.Trendyol.Commands.TestTrendyol;

internal sealed class TestTrendyolCommandHandler : IRequestHandler<TestTrendyolCommand, bool>
{
    private readonly ITenantContext _tenant;
    private readonly IProviderConnectionRepository _connections;
    private readonly ISecretProtector _secrets;
    private readonly ITrendyolClient _client;
    private readonly TrendyolOptions _opts;

    public TestTrendyolCommandHandler(
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

    public async Task<bool> Handle(TestTrendyolCommand request, CancellationToken ct)
    {
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null)
            throw new UnauthorizedException("Tenant context missing.");

        // First try by id
        var conn = await _connections.GetByIdAsync(new ProviderConnectionId(request.ConnectionId), ct);

        // Fallback: if UI sent stale id, pick tenant provider
        if (conn is null)
            conn = await _connections.GetByProviderAsync(tenantId.Value, ProviderType.Trendyol, ct);

        if (conn is null || conn.TenantId != tenantId.Value || conn.ProviderType != ProviderType.Trendyol)
            throw new NotFoundException($"Trendyol connection not found for tenant. connectionId={request.ConnectionId}");

        var sellerId = conn.ExternalAccountId ?? throw new InvalidOperationException("SellerId missing.");
        var credsJson = _secrets.Unprotect(conn.AccessToken);
        var creds = JsonSerializer.Deserialize<TrendyolCreds>(credsJson) ?? throw new InvalidOperationException("Trendyol credentials invalid.");

        var endMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var startMs = DateTimeOffset.UtcNow.AddDays(-1).ToUnixTimeMilliseconds();

        _ = await _client.GetOrdersAsync(
            apiKey: creds.ApiKey,
            apiSecret: creds.ApiSecret,
            sellerId: sellerId,
            userAgent: creds.UserAgent,
            startDateMs: startMs,
            endDateMs: endMs,
            page: 0,
            size: 1,
            orderByField: _opts.OrderByField,
            ct: ct);

        return true;
    }

    private sealed record TrendyolCreds(string ApiKey, string ApiSecret, string UserAgent);
}
