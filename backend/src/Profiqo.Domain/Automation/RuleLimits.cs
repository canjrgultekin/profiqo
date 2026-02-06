using Profiqo.Domain.Common;

namespace Profiqo.Domain.Automation;


public sealed class RuleLimits
{
    public int MaxPerCustomerPerDay { get; private set; }
    public int MaxPerCustomerTotal { get; private set; }
    public int CooldownHours { get; private set; }

    private RuleLimits()
    {
    }

    private RuleLimits(int maxPerDay, int maxTotal, int cooldownHours)
    {
        MaxPerCustomerPerDay = Guard.AgainstOutOfRange(maxPerDay, 0, 1000000, nameof(maxPerDay));
        MaxPerCustomerTotal = Guard.AgainstOutOfRange(maxTotal, 0, 1000000, nameof(maxTotal));
        CooldownHours = Guard.AgainstOutOfRange(cooldownHours, 0, 24 * 365, nameof(cooldownHours));
    }

    public static RuleLimits Create(int maxPerDay, int maxTotal, int cooldownHours)
        => new(maxPerDay, maxTotal, cooldownHours);
}