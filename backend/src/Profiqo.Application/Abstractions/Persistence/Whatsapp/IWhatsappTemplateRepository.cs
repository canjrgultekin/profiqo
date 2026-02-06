// Path: backend/src/Profiqo.Application/Abstractions/Persistence/Whatsapp/IWhatsappTemplateRepository.cs
namespace Profiqo.Application.Abstractions.Persistence.Whatsapp;

public interface IWhatsappTemplateRepository
{
    Task<WhatsappTemplateDto?> GetByIdAsync(Guid id, CancellationToken ct);

    Task<WhatsappTemplateDto?> GetByNameAsync(
        Guid tenantId,
        Guid connectionId,
        string name,
        CancellationToken ct);

    Task<IReadOnlyList<WhatsappTemplateDto>> ListAsync(
        Guid tenantId,
        Guid connectionId,
        CancellationToken ct);

    Task<Guid> UpsertAsync(WhatsappTemplateUpsertRequest req, CancellationToken ct);

    Task UpdateMetaStatusAsync(
        Guid id,
        string status,
        string? rejectionReason,
        string? metaTemplateId,
        CancellationToken ct);
}