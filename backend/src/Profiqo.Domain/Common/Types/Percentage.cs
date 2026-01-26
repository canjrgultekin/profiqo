namespace Profiqo.Domain.Common.Types;


public readonly record struct Percentage
{
    public decimal Rate { get; }

    public Percentage(decimal rate)
    {
        Rate = Guard.AgainstOutOfRange(rate, 0m, 1m, nameof(rate));
    }

    public static Percentage FromPercent(decimal percent)
        => new(percent / 100m);

    public decimal ToPercent()
        => Rate * 100m;

    public override string ToString()
        => $"{ToPercent():0.##}%";
}