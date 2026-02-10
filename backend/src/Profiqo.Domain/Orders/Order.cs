using System.Text.Json;

using Profiqo.Domain.Common;
using Profiqo.Domain.Common.Ids;
using Profiqo.Domain.Common.Types;
using Profiqo.Domain.Orders.Events;

namespace Profiqo.Domain.Orders;

public sealed class Order : AggregateRoot<OrderId>
{
    public TenantId TenantId { get; private set; }
    public CustomerId CustomerId { get; private set; }

    public SalesChannel Channel { get; private set; }
    public string? ProviderOrderId { get; private set; }

    // ✅ NEW: provider'dan gelen string status (trendyol/ikas)
    public string? ProviderOrderStatus { get; private set; }

    public OrderStatus Status { get; private set; }

    public DateTimeOffset PlacedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }

    private readonly List<OrderLine> _lines = new();
    public IReadOnlyCollection<OrderLine> Lines => _lines.AsReadOnly();

    public Money TotalAmount { get; private set; }
    public CostBreakdown? CostBreakdown { get; private set; }

    public Money NetProfit { get; private set; }
    public string CostBreakdownJson { get; private set; } = "{}";

    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    private Order() : base()
    {
        TenantId = default;
        CustomerId = default;
        TotalAmount = Money.Zero(CurrencyCode.TRY);
        PlacedAtUtc = default;
        NetProfit = Money.Zero(CurrencyCode.TRY);
        ProviderOrderStatus = null;
    }

    private Order(
        OrderId id,
        TenantId tenantId,
        CustomerId customerId,
        SalesChannel channel,
        string? providerOrderId,
        DateTimeOffset placedAtUtc,
        IReadOnlyCollection<OrderLine> lines,
        Money totalAmount,
        DateTimeOffset nowUtc) : base(id)
    {
        TenantId = tenantId;
        CustomerId = customerId;
        Channel = channel;

        ProviderOrderId = providerOrderId is null
            ? null
            : Guard.AgainstTooLong(Guard.AgainstNullOrWhiteSpace(providerOrderId, nameof(providerOrderId)), 200, nameof(providerOrderId));

        ProviderOrderStatus = null;

        PlacedAtUtc = Guard.EnsureUtc(placedAtUtc, nameof(placedAtUtc));
        _lines.AddRange(Guard.AgainstNullOrEmpty(lines, nameof(lines)));

        TotalAmount = totalAmount;

        Status = OrderStatus.Pending;

        var utc = Guard.EnsureUtc(nowUtc, nameof(nowUtc));
        CreatedAtUtc = utc;
        UpdatedAtUtc = utc;

        NetProfit = Money.Zero(totalAmount.Currency);
        CostBreakdownJson = "{}";
    }

    public static Order Create(
        TenantId tenantId,
        CustomerId customerId,
        SalesChannel channel,
        string? providerOrderId,
        DateTimeOffset placedAtUtc,
        IReadOnlyCollection<OrderLine> lines,
        Money totalAmount,
        DateTimeOffset nowUtc)
        => new(OrderId.New(), tenantId, customerId, channel, providerOrderId, placedAtUtc, lines, totalAmount, nowUtc);

    public void SetProviderOrderStatus(string? providerStatus, DateTimeOffset nowUtc)
    {
        ProviderOrderStatus = string.IsNullOrWhiteSpace(providerStatus)
            ? null
            : Guard.AgainstTooLong(providerStatus.Trim(), 120, nameof(providerStatus));

        Touch(nowUtc);
    }

    public void UpdateStatus(OrderStatus status, DateTimeOffset nowUtc)
    {
        Status = status;
        Touch(nowUtc);
    }

    public void SetCostBreakdown(CostBreakdown breakdown, DateTimeOffset nowUtc)
    {
        if (breakdown is null) throw new DomainException("breakdown cannot be null.");

        if (!breakdown.SalesAmount.Currency.Equals(TotalAmount.Currency))
            throw new BusinessRuleViolationException("currency_mismatch", "Cost breakdown currency must match order currency.");

        CostBreakdown = breakdown;
        NetProfit = breakdown.NetProfit();
        CostBreakdownJson = JsonSerializer.Serialize(breakdown);

        Touch(nowUtc);
    }

    public Money NetProfitOrZero()
        => CostBreakdown is null ? Money.Zero(TotalAmount.Currency) : CostBreakdown.NetProfit();

    public void ReassignCustomer(CustomerId customerId, DateTimeOffset nowUtc)
    {
        if (customerId.Value == Guid.Empty)
            throw new DomainException("customerId cannot be empty.");

        if (customerId.Equals(CustomerId))
            return;

        CustomerId = customerId;
        Touch(nowUtc);
    }


    public void MarkCompleted(DateTimeOffset completedAtUtc)
    {
        if (Status is OrderStatus.Cancelled or OrderStatus.Refunded)
            throw new BusinessRuleViolationException("order_invalid_state", "Cancelled or refunded order cannot be marked as completed.");

        Status = OrderStatus.Fulfilled;
        CompletedAtUtc = Guard.EnsureUtc(completedAtUtc, nameof(completedAtUtc));
        UpdatedAtUtc = CompletedAtUtc.Value;

        AddDomainEvent(new OrderCompletedDomainEvent(TenantId, Id, CustomerId, CompletedAtUtc.Value));
    }

    private void Touch(DateTimeOffset nowUtc)
        => UpdatedAtUtc = Guard.EnsureUtc(nowUtc, nameof(nowUtc));
}
