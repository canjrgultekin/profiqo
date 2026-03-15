using System.Text.Json;

using MediatR;

using Profiqo.Application.Abstractions.Crypto;
using Profiqo.Application.Abstractions.Integrations.Whatsapp;
using Profiqo.Application.Abstractions.Persistence.Repositories;
using Profiqo.Application.Abstractions.Persistence.Whatsapp;
using Profiqo.Application.Abstractions.Tenancy;
using Profiqo.Application.Common.Exceptions;
using Profiqo.Domain.Common.Ids;
using Profiqo.Domain.Integrations;

namespace Profiqo.Application.Integrations.Whatsapp.Templates.SyncWhatsappTemplates;

internal sealed class SyncWhatsappTemplatesCommandHandler : IRequestHandler<SyncWhatsappTemplatesCommand, int>
{
    private readonly ITenantContext _tenant;
    private readonly IProviderConnectionRepository _connections;
    private readonly ISecretProtector _secrets;
    private readonly IWhatsappGraphApiClient _wa;
    private readonly IWhatsappTemplateRepository _templates;

    public SyncWhatsappTemplatesCommandHandler(
        ITenantContext tenant,
        IProviderConnectionRepository connections,
        ISecretProtector secrets,
        IWhatsappGraphApiClient wa,
        IWhatsappTemplateRepository templates)
    {
        _tenant = tenant;
        _connections = connections;
        _secrets = secrets;
        _wa = wa;
        _templates = templates;
    }

    public async Task<int> Handle(SyncWhatsappTemplatesCommand request, CancellationToken ct)
    {
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null) throw new UnauthorizedException("Tenant context missing.");

        var conn = await _connections.GetByIdAsync(new ProviderConnectionId(request.ConnectionId), ct);
        if (conn is null || conn.ProviderType != ProviderType.Whatsapp)
            throw new NotFoundException("whatsapp_connection_not_found-WhatsApp connection not found.");

        var secretJson = _secrets.Unprotect(conn.AccessToken);
        var secret = WhatsappConnectionSecret.FromJson(secretJson);

        var remote = await _wa.ListMessageTemplatesAsync(secret.WabaId, ct);

        var updated = 0;
        foreach (var r in remote)
        {
            var name = WhatsappTemplateNameNormalizer.Normalize(r.Name);
            var lang = (r.Language ?? "tr").Trim().ToLowerInvariant();
            var cat = (r.Category ?? "MARKETING").Trim().ToUpperInvariant();
            var status = string.IsNullOrWhiteSpace(r.Status) ? "UNKNOWN" : r.Status;

            _ = await _templates.UpsertAsync(
                new WhatsappTemplateUpsertRequest(
                    TenantId: tenantId.Value.Value,
                    ConnectionId: conn.Id.Value,
                    Name: name,
                    Language: lang,
                    Category: cat,
                    Status: status,
                    ComponentsJson: string.IsNullOrWhiteSpace(r.ComponentsJson) ? "[]" : r.ComponentsJson,
                    MetaTemplateId: r.Id,
                    RejectionReason: r.RejectedReason),
                ct);

            updated++;
        }

        return updated;
    }
}
