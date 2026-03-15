using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Profiqo.Infrastructure.Persistence.Entities;
using Profiqo.Application.Integrations.Jobs;

namespace Profiqo.Infrastructure.Persistence.Configurations;

internal sealed class IntegrationJobConfiguration : IEntityTypeConfiguration<IntegrationJob>
{
    public void Configure(EntityTypeBuilder<IntegrationJob> builder)
    {
        builder.ToTable("integration_jobs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Kind).HasConversion<short>().IsRequired();
        builder.Property(x => x.Status).HasConversion<short>().IsRequired();

        builder.Property(x => x.PageSize).IsRequired();
        builder.Property(x => x.MaxPages).IsRequired();
        builder.Property(x => x.ProcessedItems).IsRequired();

        builder.Property(x => x.LockedBy).HasMaxLength(128);
        builder.Property(x => x.LastError).HasColumnType("text");

        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).IsRequired().IsConcurrencyToken();

        builder.HasIndex(x => new { x.Status, x.CreatedAtUtc });
        builder.HasIndex(x => x.BatchId);
        builder.HasIndex(x => new { x.TenantId, x.ConnectionId });
    }
}