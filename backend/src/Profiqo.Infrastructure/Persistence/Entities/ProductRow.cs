// Path: backend/src/Profiqo.Infrastructure/Persistence/Entities/ProductRow.cs
namespace Profiqo.Infrastructure.Persistence.Entities;

public sealed class ProductRow
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string ProviderProductId { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string? BrandId { get; private set; }
    public string? BrandName { get; private set; }
    public string? CategoryIdsJson { get; private set; }   // jsonb: ["uuid",...]
    public string? CategoriesJson { get; private set; }     // jsonb: [{id,name},...]
    public int TotalStock { get; private set; }
    public string? ProductVolumeDiscountId { get; private set; }
    public long ProviderCreatedAtMs { get; private set; }
    public long ProviderUpdatedAtMs { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    private ProductRow() { }

    public ProductRow(
        Guid id,
        Guid tenantId,
        string providerProductId,
        string name,
        string? description,
        string? brandId,
        string? brandName,
        string? categoryIdsJson,
        string? categoriesJson,
        int totalStock,
        string? productVolumeDiscountId,
        long providerCreatedAtMs,
        long providerUpdatedAtMs,
        DateTimeOffset nowUtc)
    {
        Id = id;
        TenantId = tenantId;
        ProviderProductId = providerProductId;
        Name = name;
        Description = description;
        BrandId = brandId;
        BrandName = brandName;
        CategoryIdsJson = categoryIdsJson;
        CategoriesJson = categoriesJson;
        TotalStock = totalStock;
        ProductVolumeDiscountId = productVolumeDiscountId;
        ProviderCreatedAtMs = providerCreatedAtMs;
        ProviderUpdatedAtMs = providerUpdatedAtMs;
        CreatedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    public void Update(
        string name,
        string? description,
        string? brandId,
        string? brandName,
        string? categoryIdsJson,
        string? categoriesJson,
        int totalStock,
        string? productVolumeDiscountId,
        long providerUpdatedAtMs,
        DateTimeOffset nowUtc)
    {
        Name = name;
        Description = description;
        BrandId = brandId;
        BrandName = brandName;
        CategoryIdsJson = categoryIdsJson;
        CategoriesJson = categoriesJson;
        TotalStock = totalStock;
        ProductVolumeDiscountId = productVolumeDiscountId;
        ProviderUpdatedAtMs = providerUpdatedAtMs;
        UpdatedAtUtc = nowUtc;
    }
}