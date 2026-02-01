namespace Profiqo.Infrastructure.Persistence.Entities;

public sealed class SyncAutomationRule
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public short Status { get; private set; }            // 1=active, 2=paused
    public int IntervalMinutes { get; private set; }     // 180/360/720/1440/10080
    public int PageSize { get; private set; }
    public int MaxPages { get; private set; }

    public DateTimeOffset NextRunAtUtc { get; private set; }
    public DateTimeOffset? LastEnqueuedAtUtc { get; private set; }

    public string? LockedBy { get; private set; }
    public DateTimeOffset? LockedAtUtc { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    private SyncAutomationRule() { }

    public SyncAutomationRule(Guid id, Guid tenantId, string name, short status, int intervalMinutes, int pageSize, int maxPages, DateTimeOffset nowUtc)
    {
        Id = id;
        TenantId = tenantId;
        Name = name;

        Status = status;
        IntervalMinutes = intervalMinutes;
        PageSize = pageSize;
        MaxPages = maxPages;

        NextRunAtUtc = nowUtc.AddMinutes(intervalMinutes);
        LastEnqueuedAtUtc = null;

        CreatedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    public void Activate(DateTimeOffset nowUtc) { Status = 1; UpdatedAtUtc = nowUtc; }
    public void Pause(DateTimeOffset nowUtc) { Status = 2; UpdatedAtUtc = nowUtc; }

    public void TouchScheduled(DateTimeOffset nowUtc)
    {
        LastEnqueuedAtUtc = nowUtc;
        NextRunAtUtc = nowUtc.AddMinutes(IntervalMinutes);
        LockedBy = null;
        LockedAtUtc = null;
        UpdatedAtUtc = nowUtc;
    }

    public void ReleaseLock(DateTimeOffset nowUtc)
    {
        LockedBy = null;
        LockedAtUtc = null;
        UpdatedAtUtc = nowUtc;
    }
}
