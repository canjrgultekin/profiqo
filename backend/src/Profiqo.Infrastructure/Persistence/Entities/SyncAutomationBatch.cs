namespace Profiqo.Infrastructure.Persistence.Entities;

public sealed class SyncAutomationBatch
{
    public Guid BatchId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid RuleId { get; private set; }

    public DateTimeOffset ScheduledAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private SyncAutomationBatch() { }

    public SyncAutomationBatch(Guid batchId, Guid tenantId, Guid ruleId, DateTimeOffset nowUtc)
    {
        BatchId = batchId;
        TenantId = tenantId;
        RuleId = ruleId;
        ScheduledAtUtc = nowUtc;
        CreatedAtUtc = nowUtc;
    }
}