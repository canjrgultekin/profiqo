// Path: backend/src/Profiqo.Infrastructure/Persistence/Configurations/ProductVariantConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Profiqo.Infrastructure.Persistence.Entities;

namespace Profiqo.Infrastructure.Persistence.Configurations;

internal sealed class ProductVariantConfiguration : IEntityTypeConfiguration<ProductVariantRow>
{
    public void Configure(EntityTypeBuilder<ProductVariantRow> builder)
    {
        builder.ToTable("product_variants");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.ProductId).HasColumnName("product_id").IsRequired();
        builder.Property(x => x.ProviderVariantId).HasColumnName("provider_variant_id").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Sku).HasColumnName("sku").HasMaxLength(200);
        builder.Property(x => x.HsCode).HasColumnName("hs_code").HasMaxLength(100);
        builder.Property(x => x.BarcodeListJson).HasColumnName("barcode_list_json").HasColumnType("jsonb");
        builder.Property(x => x.SellIfOutOfStock).HasColumnName("sell_if_out_of_stock");
        builder.Property(x => x.PricesJson).HasColumnName("prices_json").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.StocksJson).HasColumnName("stocks_json").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.ProviderCreatedAtMs).HasColumnName("provider_created_at_ms").IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired().IsConcurrencyToken();

        builder.HasIndex(x => new { x.TenantId, x.ProviderVariantId }).IsUnique()
            .HasDatabaseName("ux_product_variants_tenant_provider");

        builder.HasIndex(x => x.ProductId)
            .HasDatabaseName("ix_product_variants_product");

        builder.HasIndex(x => new { x.TenantId, x.Sku })
            .HasFilter("sku IS NOT NULL")
            .HasDatabaseName("ix_product_variants_sku");
    }
}