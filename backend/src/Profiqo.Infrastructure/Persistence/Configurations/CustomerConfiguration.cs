using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Profiqo.Domain.Common.Ids;
using Profiqo.Domain.Customers;
using Profiqo.Infrastructure.Persistence.Converters;

namespace Profiqo.Infrastructure.Persistence.Configurations;

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("customers");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasConversion(new StronglyTypedIdConverter<CustomerId>())
            .ValueGeneratedNever();

        builder.Property(x => x.TenantId)
            .HasConversion(new StronglyTypedIdConverter<TenantId>())
            .IsRequired();

        builder.Property(x => x.FirstName).HasMaxLength(100);
        builder.Property(x => x.LastName).HasMaxLength(100);

        builder.Property(x => x.FirstSeenAtUtc).IsRequired();
        builder.Property(x => x.LastSeenAtUtc).IsRequired();

        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).IsRequired().IsConcurrencyToken();

        builder.HasIndex(x => new { x.TenantId, x.LastSeenAtUtc });

        builder.OwnsOne(x => x.Rfm, rfm =>
        {
            rfm.Property(p => p.RecencyScore).HasColumnName("rfm_r");
            rfm.Property(p => p.FrequencyScore).HasColumnName("rfm_f");
            rfm.Property(p => p.MonetaryScore).HasColumnName("rfm_m");
            rfm.Property(p => p.Segment).HasConversion<short>().HasColumnName("rfm_segment");
            rfm.Property(p => p.ComputedAtUtc).HasColumnName("rfm_computed_at_utc");
        });

        builder.OwnsOne(x => x.AiScores, ai =>
        {
            ai.Property(p => p.Ltv12mProfit).HasColumnName("ai_ltv_12m_profit").HasColumnType("numeric(19,4)");
            ai.Property(p => p.ChurnRiskScore).HasColumnName("ai_churn_risk");
            ai.Property(p => p.NextPurchaseAtUtc).HasColumnName("ai_next_purchase_at_utc");
            ai.Property(p => p.DiscountSensitivityScore).HasColumnName("ai_discount_sensitivity");
            ai.Property(p => p.ComputedAtUtc).HasColumnName("ai_computed_at_utc");
        });

        // Identities - FK tipi düzeltildi
        builder.Navigation(x => x.Identities).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.OwnsMany(x => x.Identities, id =>
        {
            id.ToTable("customer_identities");

            // FK'yı doğru tip ve converter ile tanımla
            id.Property<CustomerId>("CustomerId")
                .HasConversion(new StronglyTypedIdConverter<CustomerId>())
                .HasColumnName("customer_id")
                .IsRequired();

            id.WithOwner().HasForeignKey("CustomerId");

            id.Property<Guid>("Id")
                .HasColumnName("id")
                .ValueGeneratedOnAdd();

            id.HasKey("Id");

            id.Property(p => p.TenantId)
                .HasConversion(new StronglyTypedIdConverter<TenantId>())
                .HasColumnName("tenant_id")
                .IsRequired();

            id.Property(p => p.Type)
                .HasConversion<short>()
                .HasColumnName("type")
                .IsRequired();

            id.Property(p => p.ValueHash)
                .HasConversion(new IdentityHashConverter())
                .HasColumnName("value_hash")
                .HasMaxLength(64)
                .IsRequired();

            id.OwnsOne(p => p.ValueEncrypted, enc =>
            {
                enc.Property(x => x.CipherText).HasColumnName("value_ciphertext").HasColumnType("text");
                enc.Property(x => x.KeyId).HasColumnName("value_key_id").HasMaxLength(128);
                enc.Property(x => x.Algorithm).HasColumnName("value_algorithm").HasMaxLength(64);
            });

            id.Navigation(p => p.ValueEncrypted).IsRequired(false);

            id.Property(p => p.SourceProvider).HasConversion<short?>().HasColumnName("source_provider");
            id.Property(p => p.SourceExternalId).HasMaxLength(200).HasColumnName("source_external_id");

            id.Property(p => p.FirstSeenAtUtc).HasColumnName("first_seen_at_utc").IsRequired();
            id.Property(p => p.LastSeenAtUtc).HasColumnName("last_seen_at_utc").IsRequired();

            id.HasIndex(p => new { p.TenantId, p.Type, p.ValueHash }).IsUnique();
            id.HasIndex("CustomerId");
        });

        // Consents - FK tipi düzeltildi
        builder.Navigation(x => x.Consents).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.OwnsMany(x => x.Consents, c =>
        {
            c.ToTable("customer_consents");

            // FK'yı doğru tip ve converter ile tanımla
            c.Property<CustomerId>("CustomerId")
                .HasConversion(new StronglyTypedIdConverter<CustomerId>())
                .HasColumnName("customer_id")
                .IsRequired();

            c.WithOwner().HasForeignKey("CustomerId");

            c.Property<Guid>("Id")
                .HasColumnName("id")
                .ValueGeneratedOnAdd();

            c.HasKey("Id");

            c.Property(p => p.Type).HasConversion<short>().HasColumnName("type").IsRequired();
            c.Property(p => p.Status).HasConversion<short>().HasColumnName("status").IsRequired();

            c.Property(p => p.Source).HasMaxLength(200).HasColumnName("source").IsRequired();
            c.Property(p => p.PolicyVersion).HasMaxLength(64).HasColumnName("policy_version").IsRequired();

            c.Property(p => p.IpAddress).HasMaxLength(64).HasColumnName("ip_address");
            c.Property(p => p.UserAgent).HasMaxLength(512).HasColumnName("user_agent");

            c.Property(p => p.ChangedAtUtc).HasColumnName("changed_at_utc").IsRequired();

            c.HasIndex("CustomerId", "Type").IsUnique();
            c.HasIndex("CustomerId", "Status");
        });
    }
}