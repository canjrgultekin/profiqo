using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Profiqo.Domain.Common.Ids;
using Profiqo.Infrastructure.Persistence.Converters;
using Profiqo.Infrastructure.Persistence.Entities;

namespace Profiqo.Infrastructure.Persistence.Configurations;

internal sealed class CustomerMergeLinkConfiguration : IEntityTypeConfiguration<CustomerMergeLink>
{
    public void Configure(EntityTypeBuilder<CustomerMergeLink> builder)
    {
        builder.ToTable("customer_merge_links");

        builder.HasKey(x => new { x.TenantId, x.SourceCustomerId });

        builder.Property(x => x.TenantId)
            .HasConversion(new StronglyTypedIdConverter<TenantId>())
            .IsRequired();

        builder.Property(x => x.SourceCustomerId)
            .HasConversion(new StronglyTypedIdConverter<CustomerId>())
            .IsRequired();

        builder.Property(x => x.CanonicalCustomerId)
            .HasConversion(new StronglyTypedIdConverter<CustomerId>())
            .IsRequired();

        builder.Property(x => x.GroupKey).HasColumnType("text").IsRequired();

        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.CanonicalCustomerId });
        builder.HasIndex(x => new { x.TenantId, x.GroupKey });

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_customer_merge_links_source_ne_canonical",
            "source_customer_id <> canonical_customer_id"));
    }
}