using MediatR;

using Profiqo.Application.Abstractions.Persistence.Whatsapp;
using Profiqo.Application.Abstractions.Tenancy;
using Profiqo.Application.Common.Exceptions;

namespace Profiqo.Application.Integrations.Whatsapp.Templates.ListWhatsappTemplates;

internal sealed class ListWhatsappTemplatesQueryHandler : IRequestHandler<ListWhatsappTemplatesQuery, IReadOnlyList<WhatsappTemplateDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IWhatsappTemplateRepository _repo;

    public ListWhatsappTemplatesQueryHandler(ITenantContext tenant, IWhatsappTemplateRepository repo)
    {
        _tenant = tenant;
        _repo = repo;
    }

    public async Task<IReadOnlyList<WhatsappTemplateDto>> Handle(ListWhatsappTemplatesQuery request, CancellationToken ct)
    {
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null) throw new UnauthorizedException("Tenant context missing.");

        return await _repo.ListAsync(tenantId.Value.Value, request.ConnectionId, ct);
    }
}