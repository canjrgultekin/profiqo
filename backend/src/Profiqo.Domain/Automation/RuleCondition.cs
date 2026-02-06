using Profiqo.Domain.Common;

namespace Profiqo.Domain.Automation;


public sealed class RuleCondition
{
    public string Field { get; private set; }
    public RuleOperator Operator { get; private set; }
    public string ValueJson { get; private set; }

    private RuleCondition()
    {
        Field = string.Empty;
        ValueJson = string.Empty;
    }

    private RuleCondition(string field, RuleOperator op, string valueJson)
    {
        Field = Guard.AgainstTooLong(Guard.AgainstNullOrWhiteSpace(field, nameof(field)), 200, nameof(field));
        Operator = op;
        ValueJson = Guard.AgainstTooLong(Guard.AgainstNullOrWhiteSpace(valueJson, nameof(valueJson)), 4096, nameof(valueJson));
    }

    public static RuleCondition Create(string field, RuleOperator op, string valueJson)
        => new(field, op, valueJson);
}