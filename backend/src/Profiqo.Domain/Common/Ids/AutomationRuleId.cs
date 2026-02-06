namespace Profiqo.Domain.Common.Ids;

public readonly record struct AutomationRuleId
{
    public Guid Value { get; }

    public AutomationRuleId(Guid value)
    {
        Value = Guard.AgainstEmpty(value, nameof(value));
    }

    public static AutomationRuleId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("N");
}