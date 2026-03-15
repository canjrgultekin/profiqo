namespace Profiqo.Application.Abstractions.Persistence.WhatsappAutomation;

public interface IWhatsappJobRepository
{
    Task<IReadOnlyList<WhatsappJobDto>> ListAsync(Guid tenantId, CancellationToken ct);
    Task<WhatsappJobDto?> GetAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<Guid> UpsertAsync(WhatsappJobDto dto, CancellationToken ct);
    Task SetActiveAsync(Guid tenantId, Guid id, bool isActive, CancellationToken ct);
    Task DeleteAsync(Guid tenantId, Guid id, CancellationToken ct);
}