namespace Profiqo.Application.Abstractions.Persistence.WhatsappAutomation;

public interface IWhatsappRuleRepository
{
    Task<IReadOnlyList<WhatsappRuleDto>> ListAsync(Guid tenantId, CancellationToken ct);
    Task<WhatsappRuleDto?> GetAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<Guid> UpsertAsync(WhatsappRuleDto dto, CancellationToken ct);
    Task DeleteAsync(Guid tenantId, Guid id, CancellationToken ct);
}