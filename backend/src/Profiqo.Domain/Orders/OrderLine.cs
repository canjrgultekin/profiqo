using Profiqo.Domain.Common;
using Profiqo.Domain.Common.Types;

namespace Profiqo.Domain.Orders;

public sealed class OrderLine
{
    public string Sku { get; private set; }
    public string ProductName { get; private set; }

    public string? ProductId { get; private set; }                    // Ikas: variant.productId (GraphQL ID)

    public string? ProductCategory { get; private set; }              // Trendyol: productCategoryId, Ikas: category name
    public string? BrandName { get; private set; }                   // Ikas: variant.brand.name
    public string[]? CategoryNames { get; private set; }             // Ikas: variant.categories[].name
    public string? Barcode { get; private set; }                      // Trendyol: barcode, Ikas: barcodeList[0]
    public Money Discount { get; private set; }                       // Trendyol: discount, Ikas: discountPrice
    public string? OrderLineItemStatusName { get; private set; }      // Trendyol: orderLineItemStatusName, Ikas: orderLineItems.status

    public int Quantity { get; private set; }
    public Money UnitPrice { get; private set; }
    public Money LineTotal { get; private set; }

    private OrderLine()
    {
        Sku = string.Empty;
        ProductName = string.Empty;
        ProductId = null;
        ProductCategory = null;
        BrandName = null;
        CategoryNames = null;
        Barcode = null;
        OrderLineItemStatusName = null;

        Quantity = 1;
        UnitPrice = Money.Zero(CurrencyCode.TRY);
        Discount = Money.Zero(CurrencyCode.TRY);
        LineTotal = Money.Zero(CurrencyCode.TRY);
    }

    public OrderLine(
        string sku,
        string productName,
        int quantity,
        Money unitPrice,
        string? productCategory = null,
        string? brandName = null,
        IReadOnlyList<string>? categoryNames = null,
        string? barcode = null,
        Money? discount = null,
        string? orderLineItemStatusName = null,
        string? productId = null)
    {
        Sku = Guard.AgainstTooLong(Guard.AgainstNullOrWhiteSpace(sku, nameof(sku)), 128, nameof(sku));
        ProductName = Guard.AgainstTooLong(Guard.AgainstNullOrWhiteSpace(productName, nameof(productName)), 300, nameof(productName));

        ProductId = string.IsNullOrWhiteSpace(productId) ? null : Guard.AgainstTooLong(productId.Trim(), 200, nameof(productId));

        Quantity = Guard.AgainstOutOfRange(quantity, 1, 1000000, nameof(quantity));
        UnitPrice = unitPrice ?? throw new DomainException("unitPrice cannot be null.");

        ProductCategory = string.IsNullOrWhiteSpace(productCategory)
            ? null
            : Guard.AgainstTooLong(productCategory.Trim(), 200, nameof(productCategory));

        BrandName = string.IsNullOrWhiteSpace(brandName)
            ? null
            : Guard.AgainstTooLong(brandName.Trim(), 200, nameof(brandName));

        if (categoryNames is null || categoryNames.Count == 0)
        {
            CategoryNames = null;
        }
        else
        {
            var cleaned = categoryNames
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(30)
                .Select(x => Guard.AgainstTooLong(x, 200, nameof(categoryNames)))
                .ToArray();

            CategoryNames = cleaned.Length == 0 ? null : cleaned;
        }

        Barcode = string.IsNullOrWhiteSpace(barcode)
            ? null
            : Guard.AgainstTooLong(barcode.Trim(), 128, nameof(barcode));

        OrderLineItemStatusName = string.IsNullOrWhiteSpace(orderLineItemStatusName)
            ? null
            : Guard.AgainstTooLong(orderLineItemStatusName.Trim(), 120, nameof(orderLineItemStatusName));

        var disc = discount ?? Money.Zero(UnitPrice.Currency);
        if (!string.Equals(disc.Currency.Value, UnitPrice.Currency.Value, StringComparison.OrdinalIgnoreCase))
            disc = Money.Zero(UnitPrice.Currency);
        if (disc.Amount < 0) disc = Money.Zero(UnitPrice.Currency);

        Discount = disc;

        // LineTotal'ı unitPrice * qty olarak bırakıyorum. Discount ayrı alan olarak raporlanacak.
        LineTotal = UnitPrice.Multiply(quantity);
    }
}
