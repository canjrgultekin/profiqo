using Profiqo.Domain.Common;

namespace Profiqo.Domain.Customers;


public sealed class CustomerAiScores
{
    public decimal Ltv12mProfit { get; private set; }
    public int ChurnRiskScore { get; private set; }
    public DateTimeOffset? NextPurchaseAtUtc { get; private set; }
    public int DiscountSensitivityScore { get; private set; }

    public DateTimeOffset ComputedAtUtc { get; private set; }

    private CustomerAiScores()
    {
    }

    private CustomerAiScores(
        decimal ltv12mProfit,
        int churnRiskScore,
        DateTimeOffset? nextPurchaseAtUtc,
        int discountSensitivityScore,
        DateTimeOffset computedAtUtc)
    {
        Ltv12mProfit = ltv12mProfit;
        ChurnRiskScore = Guard.AgainstOutOfRange(churnRiskScore, 0, 100, nameof(churnRiskScore));
        NextPurchaseAtUtc = nextPurchaseAtUtc?.ToUniversalTime();
        DiscountSensitivityScore = Guard.AgainstOutOfRange(discountSensitivityScore, 0, 100, nameof(discountSensitivityScore));
        ComputedAtUtc = Guard.EnsureUtc(computedAtUtc, nameof(computedAtUtc));
    }

    public static CustomerAiScores Create(
        decimal ltv12mProfit,
        int churnRiskScore,
        DateTimeOffset? nextPurchaseAtUtc,
        int discountSensitivityScore,
        DateTimeOffset computedAtUtc)
    {
        return new CustomerAiScores(ltv12mProfit, churnRiskScore, nextPurchaseAtUtc, discountSensitivityScore, computedAtUtc);
    }
}