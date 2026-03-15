using Profiqo.Domain.Common;

namespace Profiqo.Domain.Customers;


public sealed class CustomerRfm
{
    public int RecencyScore { get; private set; }
    public int FrequencyScore { get; private set; }
    public int MonetaryScore { get; private set; }

    public RfmSegment Segment { get; private set; }
    public DateTimeOffset ComputedAtUtc { get; private set; }

    private CustomerRfm()
    {
    }

    private CustomerRfm(int r, int f, int m, DateTimeOffset computedAtUtc)
    {
        RecencyScore = Guard.AgainstOutOfRange(r, 1, 5, nameof(r));
        FrequencyScore = Guard.AgainstOutOfRange(f, 1, 5, nameof(f));
        MonetaryScore = Guard.AgainstOutOfRange(m, 1, 5, nameof(m));

        Segment = MapSegment(RecencyScore, FrequencyScore, MonetaryScore);
        ComputedAtUtc = Guard.EnsureUtc(computedAtUtc, nameof(computedAtUtc));
    }

    public static CustomerRfm Create(int r, int f, int m, DateTimeOffset computedAtUtc)
        => new(r, f, m, computedAtUtc);

    private static RfmSegment MapSegment(int r, int f, int m)
    {
        if (r == 5 && f == 5 && m == 5) return RfmSegment.Champions;

        if (r == 1 && f == 1 && m == 1) return RfmSegment.Lost;

        if (r <= 2 && f <= 2 && m <= 2) return RfmSegment.Hibernating;

        if (r == 1 && f >= 4 && m >= 4) return RfmSegment.CantLoseThem;
        if (r == 2 && f >= 4 && m >= 4) return RfmSegment.AtRisk;

        if (r >= 4 && f >= 4 && m >= 4) return RfmSegment.LoyalCustomers;

        if (r >= 4 && f is >= 2 and <= 3 && m is >= 2 and <= 3) return RfmSegment.PotentialLoyalists;

        if (r == 5 && f == 1 && m <= 2) return RfmSegment.NewCustomers;

        if (r is >= 3 and <= 4 && f is >= 1 and <= 2 && m is >= 1 and <= 2) return RfmSegment.Promising;

        if (r == 3 && f == 3 && m == 3) return RfmSegment.NeedAttention;

        if (r is >= 2 and <= 3 && f is >= 2 and <= 3 && m is >= 2 and <= 3) return RfmSegment.AboutToSleep;

        return RfmSegment.NeedAttention;
    }
}
