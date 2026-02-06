using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Profiqo.Infrastructure.Persistence.Entities;

namespace Profiqo.Infrastructure.Persistence.Configurations;

internal sealed class SyncAutomationBatchConfiguration : IEntityTypeConfiguration<SyncAutomationBatch>
{
    public void Configure(EntityTypeBuilder<SyncAutomationBatch> builder)
    {
        builder.ToTable("sync_automation_batches");
        builder.HasKey(x => x.BatchId);

        builder.Property(x => x.BatchId).HasColumnName("batch_id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id");
        builder.Property(x => x.RuleId).HasColumnName("rule_id");
        builder.Property(x => x.ScheduledAtUtc).HasColumnName("scheduled_at_utc");
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");

        builder.HasIndex(x => new { x.TenantId, x.ScheduledAtUtc });
    }
}