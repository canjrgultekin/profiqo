using System.Text.Json;

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

    // ✅ NEW
    public string JobKindsJson { get; private set; } = "[]";  // jsonb string
    public int JitterMinutes { get; private set; }            // 0..10

    public DateTimeOffset NextRunAtUtc { get; private set; }
    public DateTimeOffset? LastEnqueuedAtUtc { get; private set; }

    public string? LockedBy { get; private set; }
    public DateTimeOffset? LockedAtUtc { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    private SyncAutomationRule() { }

    // ✅ signature backwards compatible (existing calls compile)
    public SyncAutomationRule(
        Guid id,
        Guid tenantId,
        string name,
        short status,
        int intervalMinutes,
        int pageSize,
        int maxPages,
        DateTimeOffset nowUtc,
        int jitterMinutes = 0,
        string? jobKindsJson = null)
    {
        Id = id;
        TenantId = tenantId;
        Name = name;

        Status = status;
        IntervalMinutes = intervalMinutes;
        PageSize = pageSize;
        MaxPages = maxPages;

        JitterMinutes = Math.Clamp(jitterMinutes, 0, 10);
        JobKindsJson = string.IsNullOrWhiteSpace(jobKindsJson) ? "[]" : jobKindsJson;

        NextRunAtUtc = ComputeNextRun(nowUtc);
        LastEnqueuedAtUtc = null;

        CreatedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    public void Activate(DateTimeOffset nowUtc) { Status = 1; UpdatedAtUtc = nowUtc; }
    public void Pause(DateTimeOffset nowUtc) { Status = 2; UpdatedAtUtc = nowUtc; }

    public void UpdateJobKinds(string jobKindsJson, DateTimeOffset nowUtc)
    {
        JobKindsJson = string.IsNullOrWhiteSpace(jobKindsJson) ? "[]" : jobKindsJson;
        UpdatedAtUtc = nowUtc;
    }

    public void UpdateJitter(int jitterMinutes, DateTimeOffset nowUtc)
    {
        JitterMinutes = Math.Clamp(jitterMinutes, 0, 10);
        UpdatedAtUtc = nowUtc;
    }

    public void TouchScheduled(DateTimeOffset nowUtc)
    {
        LastEnqueuedAtUtc = nowUtc;
        NextRunAtUtc = ComputeNextRun(nowUtc);
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

    public IReadOnlyList<string> GetJobKindsSafe()
    {
        if (string.IsNullOrWhiteSpace(JobKindsJson)) return Array.Empty<string>();

        try
        {
            var arr = JsonSerializer.Deserialize<string[]>(JobKindsJson);
            return arr?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct().ToArray()
                   ?? Array.Empty<string>();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private DateTimeOffset ComputeNextRun(DateTimeOffset nowUtc)
    {
        var jitter = JitterMinutes <= 0 ? 0 : Random.Shared.Next(0, JitterMinutes + 1);
        return nowUtc.AddMinutes(IntervalMinutes + jitter);
    }
}
