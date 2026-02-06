using Profiqo.Application.Integrations.Jobs;

namespace Profiqo.Infrastructure.Persistence.Entities;

public sealed class IntegrationJob
{
    public Guid Id { get; private set; }

    public Guid BatchId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ConnectionId { get; private set; }

    public IntegrationJobKind Kind { get; private set; }
    public IntegrationJobStatus Status { get; private set; }

    public int PageSize { get; private set; }
    public int MaxPages { get; private set; }

    public int ProcessedItems { get; private set; }

    public string? LockedBy { get; private set; }
    public DateTimeOffset? LockedAtUtc { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public DateTimeOffset? StartedAtUtc { get; private set; }
    public DateTimeOffset? FinishedAtUtc { get; private set; }

    public string? LastError { get; private set; }

    private IntegrationJob() { }

    public IntegrationJob(Guid id, Guid batchId, Guid tenantId, Guid connectionId, IntegrationJobKind kind, int pageSize, int maxPages)
    {
        Id = id;
        BatchId = batchId;
        TenantId = tenantId;
        ConnectionId = connectionId;
        Kind = kind;
        PageSize = pageSize;
        MaxPages = maxPages;

        Status = IntegrationJobStatus.Queued;
        ProcessedItems = 0;
        CreatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
    }

    public void MarkRunning(string workerId)
    {
        Status = IntegrationJobStatus.Running;
        LockedBy = workerId;
        LockedAtUtc = DateTimeOffset.UtcNow;
        StartedAtUtc ??= DateTimeOffset.UtcNow;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void MarkProgress(int processed)
    {
        ProcessedItems = processed;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void MarkSucceeded()
    {
        Status = IntegrationJobStatus.Succeeded;
        FinishedAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        LastError = null;
    }

    public void MarkFailed(string error)
    {
        Status = IntegrationJobStatus.Failed;
        FinishedAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        LastError = error;
    }
}
