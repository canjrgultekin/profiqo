namespace Profiqo.Application.Integrations.Jobs;

public enum IntegrationJobStatus : short
{
    Queued = 1,
    Running = 2,
    Succeeded = 3,
    Failed = 4,
    Cancelled = 5
}

public enum IntegrationJobKind : short
{
    IkasSyncCustomers = 10,
    IkasSyncOrders = 11,
    IkasSyncAbandonedCheckouts = 12,
    TrendyolSyncOrders = 20

}


public sealed record IntegrationJobCreateRequest(
    Guid BatchId,
    Guid TenantId,
    Guid ConnectionId,
    IntegrationJobKind Kind,
    int PageSize,
    int MaxPages);

public sealed record IntegrationJobDto(
    Guid JobId,
    Guid BatchId,
    Guid TenantId,
    Guid ConnectionId,
    IntegrationJobKind Kind,
    IntegrationJobStatus Status,
    int PageSize,
    int MaxPages,
    int ProcessedItems,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? FinishedAtUtc,
    string? LastError);