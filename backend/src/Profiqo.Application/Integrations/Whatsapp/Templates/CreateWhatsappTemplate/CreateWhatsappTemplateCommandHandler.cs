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

namespace Profiqo.Application.Integrations.Whatsapp.Templates.CreateWhatsappTemplate;

internal sealed class CreateWhatsappTemplateCommandHandler : IRequestHandler<CreateWhatsappTemplateCommand, Guid>
{
    private readonly ITenantContext _tenant;
    private readonly IProviderConnectionRepository _connections;
    private readonly ISecretProtector _secrets;
    private readonly IWhatsappGraphApiClient _wa;
    private readonly IWhatsappTemplateRepository _templates;

    public CreateWhatsappTemplateCommandHandler(
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

    public async Task<Guid> Handle(CreateWhatsappTemplateCommand request, CancellationToken ct)
    {
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null) throw new UnauthorizedException("Tenant context missing.");

        var conn = await _connections.GetByIdAsync(new ProviderConnectionId(request.ConnectionId), ct);
        if (conn is null || conn.ProviderType != ProviderType.Whatsapp)
            throw new NotFoundException("whatsapp_connection_not_found-WhatsApp connection not found.");

        var name = WhatsappTemplateNameNormalizer.Normalize(request.Name);
        var lang = (request.Language ?? "tr").Trim().ToLowerInvariant();
        var cat = (request.Category ?? "MARKETING").Trim().ToUpperInvariant();

        if (request.Components.ValueKind != JsonValueKind.Array)
            throw new AppValidationException(new Dictionary<string, string[]> { ["components"] = new[] { "Components must be a JSON array." } });

        var secretJson = _secrets.Unprotect(conn.AccessToken);
        var secret = WhatsappConnectionSecret.FromJson(secretJson);

        var created = await _wa.CreateMessageTemplateAsync(
            wabaId: secret.WabaId,
            name: name,
            language: lang,
            category: cat,
            components: request.Components,
            ct: ct);

        var status = string.IsNullOrWhiteSpace(created.Status) ? "PENDING" : created.Status;

        var upsertId = await _templates.UpsertAsync(
            new WhatsappTemplateUpsertRequest(
                TenantId: tenantId.Value.Value,
                ConnectionId: conn.Id.Value,
                Name: name,
                Language: lang,
                Category: cat,
                Status: status,
                ComponentsJson: request.Components.GetRawText(),
                MetaTemplateId: created.TemplateId,
                RejectionReason: null),
            ct);

        return upsertId;
    }
}
