namespace Profiqo.Infrastructure.Persistence.Entities;

public sealed class SyncAutomationRuleConnection
{
    public Guid TenantId { get; private set; }
    public Guid RuleId { get; private set; }
    public Guid ConnectionId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    private SyncAutomationRuleConnection() { }

    public SyncAutomationRuleConnection(Guid tenantId, Guid ruleId, Guid connectionId, DateTimeOffset nowUtc)
    {
        TenantId = tenantId;
        RuleId = ruleId;
        ConnectionId = connectionId;
        CreatedAtUtc = nowUtc;
    }
}