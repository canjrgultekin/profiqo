using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Profiqo.Domain.Common.Ids;
using Profiqo.Infrastructure.Persistence.Converters;
using Profiqo.Infrastructure.Persistence.Entities;

namespace Profiqo.Infrastructure.Persistence.Configurations;

internal sealed class CustomerMergeDecisionConfiguration : IEntityTypeConfiguration<CustomerMergeDecision>
{
    public void Configure(EntityTypeBuilder<CustomerMergeDecision> builder)
    {
        builder.ToTable("customer_merge_decisions");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.TenantId)
            .HasConversion(new StronglyTypedIdConverter<TenantId>())
            .IsRequired();

        builder.Property(x => x.GroupKey).HasColumnType("text").IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<short>()
            .IsRequired();

        builder.Property(x => x.SuggestionUpdatedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(x => x.CanonicalCustomerId);

        builder.Property(x => x.DecidedAtUtc).IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.GroupKey }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.Status, x.DecidedAtUtc });
    }
}