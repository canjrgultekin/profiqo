// Path: backend/src/Profiqo.Application/Integrations/Hepsiburada/Commands/TestHepsiburada/TestHepsiburadaCommandHandler.cs
using System.Text.Json;

using MediatR;

using Microsoft.Extensions.Options;

using Profiqo.Application.Abstractions.Crypto;
using Profiqo.Application.Abstractions.Integrations.Hepsiburada;
using Profiqo.Application.Abstractions.Persistence.Repositories;
using Profiqo.Application.Abstractions.Tenancy;
using Profiqo.Application.Common.Exceptions;
using Profiqo.Domain.Common.Ids;
using Profiqo.Domain.Integrations;

namespace Profiqo.Application.Integrations.Hepsiburada.Commands.TestHepsiburada;

internal sealed class TestHepsiburadaCommandHandler : IRequestHandler<TestHepsiburadaCommand, bool>
{
    private readonly ITenantContext _tenant;
    private readonly IProviderConnectionRepository _connections;
    private readonly ISecretProtector _secrets;
    private readonly IHepsiburadaClient _client;
    private readonly HepsiburadaOptions _opts;

    public TestHepsiburadaCommandHandler(
        ITenantContext tenant,
        IProviderConnectionRepository connections,
        ISecretProtector secrets,
        IHepsiburadaClient client,
        IOptions<HepsiburadaOptions> opts)
    {
        _tenant = tenant;
        _connections = connections;
        _secrets = secrets;
        _client = client;
        _opts = opts.Value;
    }

    public async Task<bool> Handle(TestHepsiburadaCommand request, CancellationToken ct)
    {
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null)
            throw new UnauthorizedException("Tenant context missing.");

        var conn = await _connections.GetByIdAsync(new ProviderConnectionId(request.ConnectionId), ct);

        if (conn is null)
            conn = await _connections.GetByProviderAsync(tenantId.Value, ProviderType.Hepsiburada, ct);

        if (conn is null || conn.TenantId != tenantId.Value || conn.ProviderType != ProviderType.Hepsiburada)
            throw new NotFoundException($"Hepsiburada connection not found for tenant. connectionId={request.ConnectionId}");

        var merchantId = conn.ExternalAccountId ?? throw new InvalidOperationException("MerchantId missing.");
        var credsJson = _secrets.Unprotect(conn.AccessToken);
        var creds = JsonSerializer.Deserialize<HepsiburadaCreds>(credsJson) ?? throw new InvalidOperationException("Hepsiburada credentials invalid.");

        var endDate = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm");
        var beginDate = DateTimeOffset.UtcNow.AddDays(-1).ToString("yyyy-MM-dd HH:mm");

        using var doc = await _client.GetPaidOrdersAsync(
            username: creds.Username,
            password: creds.Password,
            merchantId: merchantId,
            offset: 0,
            limit: 1,
            beginDate: beginDate,
            endDate: endDate,
            ct: ct);

        return true;
    }

    private sealed record HepsiburadaCreds(string Username, string Password);
}