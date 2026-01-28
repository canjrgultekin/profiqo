using System.Text.Json;

using MediatR;

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

    public TestTrendyolCommandHandler(
        ITenantContext tenant,
        IProviderConnectionRepository connections,
        ISecretProtector secrets,
        ITrendyolClient client)
    {
        _tenant = tenant;
        _connections = connections;
        _secrets = secrets;
        _client = client;
    }

    public async Task<bool> Handle(TestTrendyolCommand request, CancellationToken ct)
    {
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null) throw new UnauthorizedException("Tenant context missing.");

        var conn = await _connections.GetByIdAsync(new ProviderConnectionId(request.ConnectionId), ct);
        if (conn is null || conn.TenantId != tenantId.Value || conn.ProviderType != ProviderType.Trendyol)
            throw new NotFoundException("Trendyol connection not found.");

        var supplierId = conn.ExternalAccountId ?? throw new InvalidOperationException("SupplierId missing on connection.");
        var credsJson = _secrets.Unprotect(conn.AccessToken);
        var creds = JsonSerializer.Deserialize<TrendyolCreds>(credsJson) ?? throw new InvalidOperationException("Trendyol credentials invalid.");

        var end = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var start = DateTimeOffset.UtcNow.AddDays(-1).ToUnixTimeMilliseconds();

        _ = await _client.GetOrdersAsync(creds.ApiKey, creds.ApiSecret, supplierId, page: 0, size: 1, status: "Created", startDateMs: start, endDateMs: end, ct);
        return true;
    }

    private sealed record TrendyolCreds(string ApiKey, string ApiSecret);
}
