using Profiqo.Domain.Common;

namespace Profiqo.Domain.Automation;


public sealed class GoalDefinition
{
    public GoalType Type { get; private set; }
    public int AttributionWindowDays { get; private set; }

    private GoalDefinition()
    {
    }

    private GoalDefinition(GoalType type, int attributionWindowDays)
    {
        Type = type;
        AttributionWindowDays = Guard.AgainstOutOfRange(attributionWindowDays, 0, 365, nameof(attributionWindowDays));
    }

    public static GoalDefinition Create(GoalType type, int attributionWindowDays)
        => new(type, attributionWindowDays);
}