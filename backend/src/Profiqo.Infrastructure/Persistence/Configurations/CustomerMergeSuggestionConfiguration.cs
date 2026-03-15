// Path: backend/src/Profiqo.Infrastructure/Persistence/Configurations/CustomerMergeSuggestionConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Profiqo.Infrastructure.Persistence.Entities;

namespace Profiqo.Infrastructure.Persistence.Configurations;

internal sealed class CustomerMergeSuggestionConfiguration : IEntityTypeConfiguration<CustomerMergeSuggestion>
{
    public void Configure(EntityTypeBuilder<CustomerMergeSuggestion> builder)
    {
        builder.ToTable("customer_merge_suggestions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(x => x.GroupKey)
            .HasColumnName("group_key")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(x => x.Confidence)
            .HasColumnName("confidence")
            .HasColumnType("numeric(5,4)")
            .IsRequired();

        builder.Property(x => x.NormalizedName)
            .HasColumnName("normalized_name")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(x => x.Rationale)
            .HasColumnName("rationale")
            .HasColumnType("text");

        builder.Property(x => x.PayloadJson)
            .HasColumnName("payload_json")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .IsRequired();

        builder.Property(x => x.ExpiresAtUtc)
            .HasColumnName("expires_at_utc")
            .IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.GroupKey }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.UpdatedAtUtc });
        builder.HasIndex(x => x.ExpiresAtUtc);
    }
}
