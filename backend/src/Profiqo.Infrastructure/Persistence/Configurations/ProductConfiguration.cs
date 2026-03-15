// Path: backend/src/Profiqo.Infrastructure/Persistence/Configurations/ProductConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Profiqo.Infrastructure.Persistence.Entities;

namespace Profiqo.Infrastructure.Persistence.Configurations;

internal sealed class ProductConfiguration : IEntityTypeConfiguration<ProductRow>
{
    public void Configure(EntityTypeBuilder<ProductRow> builder)
    {
        builder.ToTable("products");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.ProviderProductId).HasColumnName("provider_product_id").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(500).IsRequired();
        builder.Property(x => x.Description).HasColumnName("description").HasColumnType("text");
        builder.Property(x => x.BrandId).HasColumnName("brand_id").HasMaxLength(200);
        builder.Property(x => x.BrandName).HasColumnName("brand_name").HasMaxLength(300);
        builder.Property(x => x.CategoryIdsJson).HasColumnName("category_ids_json").HasColumnType("jsonb");
        builder.Property(x => x.CategoriesJson).HasColumnName("categories_json").HasColumnType("jsonb");
        builder.Property(x => x.TotalStock).HasColumnName("total_stock").IsRequired();
        builder.Property(x => x.ProductVolumeDiscountId).HasColumnName("product_volume_discount_id").HasMaxLength(200);
        builder.Property(x => x.ProviderCreatedAtMs).HasColumnName("provider_created_at_ms").IsRequired();
        builder.Property(x => x.ProviderUpdatedAtMs).HasColumnName("provider_updated_at_ms").IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired().IsConcurrencyToken();

        builder.HasIndex(x => new { x.TenantId, x.ProviderProductId }).IsUnique()
            .HasDatabaseName("ux_products_tenant_provider");

        builder.HasIndex(x => new { x.TenantId, x.Name })
            .HasDatabaseName("ix_products_tenant_name");

        builder.HasIndex(x => new { x.TenantId, x.ProviderUpdatedAtMs })
            .IsDescending(false, true)
            .HasDatabaseName("ix_products_tenant_updated");
    }
}