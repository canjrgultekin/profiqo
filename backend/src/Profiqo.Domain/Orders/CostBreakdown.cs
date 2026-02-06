using Profiqo.Domain.Common.Types;

namespace Profiqo.Domain.Orders;


public sealed class CostBreakdown
{
    public Money SalesAmount { get; private set; }
    public Money CogsAmount { get; private set; }
    public Money ShippingCost { get; private set; }
    public Money PlatformCommission { get; private set; }
    public Money PaymentFee { get; private set; }
    public Money CouponDiscount { get; private set; }
    public Money RefundProcessingCost { get; private set; }
    public Money ShippingRevenue { get; private set; }

    private CostBreakdown()
    {
        SalesAmount = default;
        CogsAmount = default;
        ShippingCost = default;
        PlatformCommission = default;
        PaymentFee = default;
        CouponDiscount = default;
        RefundProcessingCost = default;
        ShippingRevenue = default;
    }

    public CostBreakdown(
        Money salesAmount,
        Money cogsAmount,
        Money shippingCost,
        Money platformCommission,
        Money paymentFee,
        Money couponDiscount,
        Money refundProcessingCost,
        Money shippingRevenue)
    {
        SalesAmount = salesAmount;
        CogsAmount = cogsAmount;
        ShippingCost = shippingCost;
        PlatformCommission = platformCommission;
        PaymentFee = paymentFee;
        CouponDiscount = couponDiscount;
        RefundProcessingCost = refundProcessingCost;
        ShippingRevenue = shippingRevenue;

        EnsureSameCurrency();
    }

    public Money NetProfit()
    {
        EnsureSameCurrency();

        return SalesAmount
            .Subtract(CogsAmount)
            .Subtract(ShippingCost)
            .Subtract(PlatformCommission)
            .Subtract(PaymentFee)
            .Subtract(CouponDiscount)
            .Subtract(RefundProcessingCost)
            .Add(ShippingRevenue);
    }

    private void EnsureSameCurrency()
    {
        var currency = SalesAmount.Currency;

        _ = CogsAmount.Currency.Equals(currency) ? true : throw new InvalidOperationException("Currency mismatch in CostBreakdown.");
        _ = ShippingCost.Currency.Equals(currency) ? true : throw new InvalidOperationException("Currency mismatch in CostBreakdown.");
        _ = PlatformCommission.Currency.Equals(currency) ? true : throw new InvalidOperationException("Currency mismatch in CostBreakdown.");
        _ = PaymentFee.Currency.Equals(currency) ? true : throw new InvalidOperationException("Currency mismatch in CostBreakdown.");
        _ = CouponDiscount.Currency.Equals(currency) ? true : throw new InvalidOperationException("Currency mismatch in CostBreakdown.");
        _ = RefundProcessingCost.Currency.Equals(currency) ? true : throw new InvalidOperationException("Currency mismatch in CostBreakdown.");
        _ = ShippingRevenue.Currency.Equals(currency) ? true : throw new InvalidOperationException("Currency mismatch in CostBreakdown.");
    }
}
