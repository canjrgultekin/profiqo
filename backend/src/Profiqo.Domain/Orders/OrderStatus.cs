namespace Profiqo.Domain.Orders;

public enum OrderStatus
{
    Pending = 1,
    Paid = 2,
    Fulfilled = 3,
    Cancelled = 4,
    Refunded = 5
}