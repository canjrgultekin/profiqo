// Path: backend/src/Profiqo.Infrastructure/Persistence/Entities/ProductVariantRow.cs
namespace Profiqo.Infrastructure.Persistence.Entities;

public sealed class ProductVariantRow
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProductId { get; private set; }
    public string ProviderVariantId { get; private set; } = string.Empty;
    public string? Sku { get; private set; }
    public string? HsCode { get; private set; }
    public string? BarcodeListJson { get; private set; }    // jsonb: ["barcode",...]
    public bool? SellIfOutOfStock { get; private set; }
    public string PricesJson { get; private set; } = "[]";  // jsonb: [{buyPrice,sellPrice,...}]
    public string StocksJson { get; private set; } = "[]";  // jsonb: [{stockCount,id,...}]
    public long ProviderCreatedAtMs { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    private ProductVariantRow() { }

    public ProductVariantRow(
        Guid id,
        Guid tenantId,
        Guid productId,
        string providerVariantId,
        string? sku,
        string? hsCode,
        string? barcodeListJson,
        bool? sellIfOutOfStock,
        string pricesJson,
        string stocksJson,
        long providerCreatedAtMs,
        DateTimeOffset nowUtc)
    {
        Id = id;
        TenantId = tenantId;
        ProductId = productId;
        ProviderVariantId = providerVariantId;
        Sku = sku;
        HsCode = hsCode;
        BarcodeListJson = barcodeListJson;
        SellIfOutOfStock = sellIfOutOfStock;
        PricesJson = pricesJson;
        StocksJson = stocksJson;
        ProviderCreatedAtMs = providerCreatedAtMs;
        CreatedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    public void Update(
        string? sku,
        string? hsCode,
        string? barcodeListJson,
        bool? sellIfOutOfStock,
        string pricesJson,
        string stocksJson,
        DateTimeOffset nowUtc)
    {
        Sku = sku;
        HsCode = hsCode;
        BarcodeListJson = barcodeListJson;
        SellIfOutOfStock = sellIfOutOfStock;
        PricesJson = pricesJson;
        StocksJson = stocksJson;
        UpdatedAtUtc = nowUtc;
    }
}