using Profiqo.Domain.Common;
using Profiqo.Domain.Common.Ids;

namespace Profiqo.Domain.Automation;

public sealed class AutomationRule : AggregateRoot<AutomationRuleId>
{
    public TenantId TenantId { get; private set; }

    public string Name { get; private set; }
    public string Description { get; private set; }

    public AutomationRuleStatus Status { get; private set; }

    public TriggerDefinition Trigger { get; private set; }
    public DelayDefinition Delay { get; private set; }
    public RuleLimits Limits { get; private set; }
    public GoalDefinition Goal { get; private set; }

    private readonly List<RuleCondition> _conditions = new();
    public IReadOnlyCollection<RuleCondition> Conditions => _conditions.AsReadOnly();

    private readonly List<RuleAction> _actions = new();
    public IReadOnlyCollection<RuleAction> Actions => _actions.AsReadOnly();

    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    private AutomationRule() : base()
    {
        TenantId = default;
        Name = string.Empty;
        Description = string.Empty;
        Trigger = null!;
        Delay = null!;
        Limits = null!;
        Goal = null!;
    }

    private AutomationRule(
        AutomationRuleId id,
        TenantId tenantId,
        string name,
        string description,
        TriggerDefinition trigger,
        IEnumerable<RuleCondition> conditions,
        DelayDefinition delay,
        IEnumerable<RuleAction> actions,
        RuleLimits limits,
        GoalDefinition goal,
        DateTimeOffset nowUtc) : base(id)
    {
        TenantId = tenantId;

        Name = Guard.AgainstTooLong(Guard.AgainstNullOrWhiteSpace(name, nameof(name)), 200, nameof(name));
        Description = Guard.AgainstTooLong(Guard.AgainstNullOrWhiteSpace(description, nameof(description)), 2000, nameof(description));

        Trigger = trigger ?? throw new DomainException("trigger cannot be null.");
        Delay = delay ?? throw new DomainException("delay cannot be null.");
        Limits = limits ?? throw new DomainException("limits cannot be null.");
        Goal = goal ?? throw new DomainException("goal cannot be null.");

        _conditions.AddRange(conditions ?? Enumerable.Empty<RuleCondition>());
        _actions.AddRange(Guard.AgainstNullOrEmpty(actions ?? Array.Empty<RuleAction>(), nameof(actions)));

        Status = AutomationRuleStatus.Draft;

        CreatedAtUtc = Guard.EnsureUtc(nowUtc, nameof(nowUtc));
        UpdatedAtUtc = CreatedAtUtc;
    }

    public static AutomationRule Create(
        TenantId tenantId,
        string name,
        string description,
        TriggerDefinition trigger,
        IEnumerable<RuleCondition> conditions,
        DelayDefinition delay,
        IEnumerable<RuleAction> actions,
        RuleLimits limits,
        GoalDefinition goal,
        DateTimeOffset nowUtc)
    {
        return new AutomationRule(AutomationRuleId.New(), tenantId, name, description, trigger, conditions, delay, actions, limits, goal, nowUtc);
    }

    public void Activate(DateTimeOffset nowUtc)
    {
        if (_actions.Count == 0)
            throw new BusinessRuleViolationException("rule_actions_required", "Automation rule must have at least one action.");

        Status = AutomationRuleStatus.Active;
        Touch(nowUtc);
    }

    public void Pause(DateTimeOffset nowUtc)
    {
        Status = AutomationRuleStatus.Paused;
        Touch(nowUtc);
    }

    public void SetDraft(DateTimeOffset nowUtc)
    {
        Status = AutomationRuleStatus.Draft;
        Touch(nowUtc);
    }

    public void Rename(string name, string description, DateTimeOffset nowUtc)
    {
        Name = Guard.AgainstTooLong(Guard.AgainstNullOrWhiteSpace(name, nameof(name)), 200, nameof(name));
        Description = Guard.AgainstTooLong(Guard.AgainstNullOrWhiteSpace(description, nameof(description)), 2000, nameof(description));
        Touch(nowUtc);
    }

    public void ReplaceDefinition(
        TriggerDefinition trigger,
        IEnumerable<RuleCondition> conditions,
        DelayDefinition delay,
        IEnumerable<RuleAction> actions,
        RuleLimits limits,
        GoalDefinition goal,
        DateTimeOffset nowUtc)
    {
        Trigger = trigger ?? throw new DomainException("trigger cannot be null.");
        Delay = delay ?? throw new DomainException("delay cannot be null.");
        Limits = limits ?? throw new DomainException("limits cannot be null.");
        Goal = goal ?? throw new DomainException("goal cannot be null.");

        _conditions.Clear();
        _conditions.AddRange(conditions ?? Enumerable.Empty<RuleCondition>());

        _actions.Clear();
        _actions.AddRange(Guard.AgainstNullOrEmpty(actions ?? Array.Empty<RuleAction>(), nameof(actions)));

        Touch(nowUtc);
    }

    private void Touch(DateTimeOffset nowUtc)
        => UpdatedAtUtc = Guard.EnsureUtc(nowUtc, nameof(nowUtc));
}
