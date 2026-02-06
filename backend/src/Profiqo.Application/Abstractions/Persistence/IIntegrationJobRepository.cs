using Profiqo.Application.Integrations.Jobs;

namespace Profiqo.Application.Abstractions.Persistence;

public interface IIntegrationJobRepository
{
    Task<Guid> CreateAsync(IntegrationJobCreateRequest req, CancellationToken ct);

    Task<IntegrationJobDto?> GetAsync(Guid jobId, CancellationToken ct);

    Task<IReadOnlyList<IntegrationJobDto>> ListByBatchAsync(Guid batchId, CancellationToken ct);

    // Worker side
    Task<IntegrationJobDto?> TryClaimNextAsync(string workerId, CancellationToken ct);

    Task MarkProgressAsync(Guid jobId, int processedItems, CancellationToken ct);

    Task MarkSucceededAsync(Guid jobId, CancellationToken ct);

    Task MarkFailedAsync(Guid jobId, string error, CancellationToken ct);
}