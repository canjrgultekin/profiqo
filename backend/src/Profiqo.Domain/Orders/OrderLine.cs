using Profiqo.Domain.Common;
using Profiqo.Domain.Common.Types;

namespace Profiqo.Domain.Orders;



public sealed class OrderLine
{
    public string Sku { get; private set; }
    public string ProductName { get; private set; }

    public int Quantity { get; private set; }
    public Money UnitPrice { get; private set; }
    public Money LineTotal { get; private set; }

    private OrderLine()
    {
        Sku = string.Empty;
        ProductName = string.Empty;
        UnitPrice = default;
        LineTotal = default;
    }

    public OrderLine(string sku, string productName, int quantity, Money unitPrice)
    {
        Sku = Guard.AgainstTooLong(Guard.AgainstNullOrWhiteSpace(sku, nameof(sku)), 128, nameof(sku));
        ProductName = Guard.AgainstTooLong(Guard.AgainstNullOrWhiteSpace(productName, nameof(productName)), 300, nameof(productName));
        Quantity = Guard.AgainstOutOfRange(quantity, 1, 1000000, nameof(quantity));
        UnitPrice = unitPrice;

        LineTotal = unitPrice.Multiply(quantity);
    }
}