using MediatR;

using Profiqo.Application.Abstractions.Crypto;
using Profiqo.Application.Abstractions.Integrations.Ikas;
using Profiqo.Application.Abstractions.Persistence.Repositories;
using Profiqo.Application.Abstractions.Tenancy;
using Profiqo.Application.Common.Exceptions;
using Profiqo.Domain.Integrations;

namespace Profiqo.Application.Integrations.Ikas.Commands.TestIkas;

internal sealed class TestIkasCommandHandler : IRequestHandler<TestIkasCommand, string>
{
    private readonly ITenantContext _tenant;
    private readonly IProviderConnectionRepository _connections;
    private readonly ISecretProtector _secrets;
    private readonly IIkasGraphqlClient _ikas;

    public TestIkasCommandHandler(
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

    public async Task<string> Handle(TestIkasCommand request, CancellationToken ct)
    {
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null)
            throw new UnauthorizedException("Tenant context missing.");

        var conn = await _connections.GetByIdAsync(new Profiqo.Domain.Common.Ids.ProviderConnectionId(request.ConnectionId), ct);
        if (conn is null || conn.ProviderType != ProviderType.Ikas || conn.TenantId != tenantId.Value)
            throw new NotFoundException("Ikas connection not found.");

        var token = _secrets.Unprotect(conn.AccessToken);

        // me { id } dokümanda var
        var meId = await _ikas.MeAsync(token, ct);
        return meId;
    }
}