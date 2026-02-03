namespace Profiqo.Application.Abstractions.Persistence.WhatsappAutomation;

public interface IWhatsappTemplateDraftRepository
{
    Task<IReadOnlyList<WhatsappTemplateDraftDto>> ListAsync(Guid tenantId, CancellationToken ct);
    Task<WhatsappTemplateDraftDto?> GetAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<Guid> UpsertAsync(Guid tenantId, Guid? id, string name, string language, string category, string componentsJson, CancellationToken ct);
    Task DeleteAsync(Guid tenantId, Guid id, CancellationToken ct);
}