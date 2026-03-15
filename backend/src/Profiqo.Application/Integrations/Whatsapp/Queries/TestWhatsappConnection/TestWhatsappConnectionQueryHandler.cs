using MediatR;

using Microsoft.Extensions.Options;

using System.Text.Json;

using Profiqo.Application.Abstractions.Crypto;
using Profiqo.Application.Abstractions.Integrations.Whatsapp;
using Profiqo.Application.Abstractions.Persistence.Repositories;
using Profiqo.Application.Abstractions.Tenancy;
using Profiqo.Application.Common.Exceptions;
using Profiqo.Domain.Common.Ids;
using Profiqo.Domain.Integrations;

namespace Profiqo.Application.Integrations.Whatsapp.Queries.TestWhatsappConnection;

internal sealed class TestWhatsappConnectionQueryHandler : IRequestHandler<TestWhatsappConnectionQuery, object>
{
    private readonly ITenantContext _tenant;
    private readonly IProviderConnectionRepository _connections;
    private readonly ISecretProtector _secrets;
    private readonly WhatsappIntegrationOptions _opt;
    private readonly IWhatsappCloudValidator _validator;

    public TestWhatsappConnectionQueryHandler(
        ITenantContext tenant,
        IProviderConnectionRepository connections,
        ISecretProtector secrets,
        IOptions<WhatsappIntegrationOptions> opt,
        IWhatsappCloudValidator validator)
    {
        _tenant = tenant;
        _connections = connections;
        _secrets = secrets;
        _opt = opt.Value;
        _validator = validator;
    }

    public async Task<object> Handle(TestWhatsappConnectionQuery request, CancellationToken ct)
    {
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null) throw new UnauthorizedException("Tenant context missing.");

        var conn = await _connections.GetByIdAsync(new ProviderConnectionId(request.ConnectionId), ct);
        if (conn is null || conn.ProviderType != ProviderType.Whatsapp)
            throw new NotFoundException("whatsapp_connection_not_found-WhatsApp connection not found.");

        var effectiveTestMode = _opt.ForceTestMode || conn.IsTestMode;

        if (effectiveTestMode)
        {
            return new
            {
                ok = true,
                mode = "TEST",
                verifiedName = "Profiqo Dummy Business",
                displayPhoneNumber = "+90 555 000 0000"
            };
        }

        var secretJson = _secrets.Unprotect(conn.AccessToken);
        var secret = WhatsappCredentialSecret.FromJson(secretJson);

        var (ok2, verified, display, rawError) = await _validator.ValidateAsync(secret.AccessToken, secret.PhoneNumberId, ct);

        return new
        {
            ok = ok2,
            mode = "PROD",
            verifiedName = verified,
            displayPhoneNumber = display,
            error = rawError
        };
    }
}
