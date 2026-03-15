using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Profiqo.Infrastructure.Persistence.Entities;

namespace Profiqo.Infrastructure.Persistence.Configurations;

internal sealed class AbandonedCheckoutConfiguration : IEntityTypeConfiguration<AbandonedCheckout>
{
    public void Configure(EntityTypeBuilder<AbandonedCheckout> builder)
    {
        builder.ToTable("abandoned_checkouts");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.ProviderType).HasColumnName("provider_type").IsRequired();
        builder.Property(x => x.ExternalId).HasColumnName("external_id").HasColumnType("text").IsRequired();

        builder.Property(x => x.CustomerEmail).HasColumnName("customer_email").HasColumnType("text");
        builder.Property(x => x.CustomerPhone).HasColumnName("customer_phone").HasColumnType("text");

        builder.Property(x => x.LastActivityDateMs).HasColumnName("last_activity_date_ms").IsRequired();

        builder.Property(x => x.CurrencyCode).HasColumnName("currency_code").HasColumnType("text");
        builder.Property(x => x.TotalFinalPrice).HasColumnName("total_final_price").HasColumnType("numeric(19,4)");
        builder.Property(x => x.Status).HasColumnName("status").HasColumnType("text");

        builder.Property(x => x.PayloadJson).HasColumnName("payload_json").HasColumnType("jsonb").IsRequired();

        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired().IsConcurrencyToken();

        builder.HasIndex(x => new { x.TenantId, x.ProviderType, x.ExternalId }).IsUnique()
            .HasDatabaseName("ux_abandoned_checkouts_tenant_provider_external");

        builder.HasIndex(x => new { x.TenantId, x.LastActivityDateMs })
            .HasDatabaseName("ix_abandoned_checkouts_tenant_last_activity");
    }
}
