using Profiqo.Domain.Common;

namespace Profiqo.Domain.Automation;


public sealed class DelayDefinition
{
    public int Value { get; private set; }
    public DelayUnit Unit { get; private set; }

    private DelayDefinition()
    {
    }

    private DelayDefinition(int value, DelayUnit unit)
    {
        Value = Guard.AgainstOutOfRange(value, 0, 3650, nameof(value));
        Unit = unit;
    }

    public static DelayDefinition Create(int value, DelayUnit unit)
        => new(value, unit);
}