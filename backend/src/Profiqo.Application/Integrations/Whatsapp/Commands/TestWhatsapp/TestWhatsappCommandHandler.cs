using System.Text.Json;

using MediatR;

using Profiqo.Application.Abstractions.Crypto;
using Profiqo.Application.Abstractions.Integrations.Whatsapp;
using Profiqo.Application.Abstractions.Persistence.Repositories;
using Profiqo.Application.Abstractions.Tenancy;
using Profiqo.Application.Common.Exceptions;
using Profiqo.Domain.Common.Ids;
using Profiqo.Domain.Integrations;

namespace Profiqo.Application.Integrations.Whatsapp.Commands.TestWhatsapp;

internal sealed class TestWhatsappCommandHandler : IRequestHandler<TestWhatsappCommand, WhatsappPhoneNumberInfo>
{
    private readonly ITenantContext _tenant;
    private readonly IProviderConnectionRepository _connections;
    private readonly ISecretProtector _secrets;
    private readonly IWhatsappGraphApiClient _wa;

    public TestWhatsappCommandHandler(
        ITenantContext tenant,
        IProviderConnectionRepository connections,
        ISecretProtector secrets,
        IWhatsappGraphApiClient wa)
    {
        _tenant = tenant;
        _connections = connections;
        _secrets = secrets;
        _wa = wa;
    }

    public async Task<WhatsappPhoneNumberInfo> Handle(TestWhatsappCommand request, CancellationToken ct)
    {
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null) throw new UnauthorizedException("Tenant context missing.");

        var conn = await _connections.GetByIdAsync(new ProviderConnectionId(request.ConnectionId), ct);
        if (conn is null || conn.ProviderType != ProviderType.Whatsapp)
            throw new NotFoundException("whatsapp_connection_not_found-WhatsApp connection not found.");

        var secretJson = _secrets.Unprotect(conn.AccessToken);
        var secret = WhatsappConnectionSecret.FromJson(secretJson);

        try
        {
            return await _wa.GetPhoneNumberInfoAsync(secret.PhoneNumberId, ct);
        }
        catch (Exception ex)
        {
            throw new ExternalServiceAuthException("whatsapp", $"Test failed. {ex.Message}");
        }
    }
}
