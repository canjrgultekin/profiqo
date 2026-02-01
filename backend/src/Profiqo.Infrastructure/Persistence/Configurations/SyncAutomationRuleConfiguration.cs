using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Profiqo.Infrastructure.Persistence.Entities;

namespace Profiqo.Infrastructure.Persistence.Configurations;

internal sealed class SyncAutomationRuleConfiguration : IEntityTypeConfiguration<SyncAutomationRule>
{
    public void Configure(EntityTypeBuilder<SyncAutomationRule> builder)
    {
        builder.ToTable("sync_automation_rules");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id");
        builder.Property(x => x.Name).HasColumnName("name");

        builder.Property(x => x.Status).HasColumnName("status");
        builder.Property(x => x.IntervalMinutes).HasColumnName("interval_minutes");
        builder.Property(x => x.PageSize).HasColumnName("page_size");
        builder.Property(x => x.MaxPages).HasColumnName("max_pages");

        // ✅ NEW
        builder.Property(x => x.JobKindsJson)
            .HasColumnName("job_kinds")
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'[]'::jsonb");

        builder.Property(x => x.JitterMinutes)
            .HasColumnName("jitter_minutes")
            .HasDefaultValue(0);

        builder.Property(x => x.NextRunAtUtc).HasColumnName("next_run_at_utc");
        builder.Property(x => x.LastEnqueuedAtUtc).HasColumnName("last_enqueued_at_utc");

        builder.Property(x => x.LockedBy).HasColumnName("locked_by");
        builder.Property(x => x.LockedAtUtc).HasColumnName("locked_at_utc");

        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");

        builder.HasIndex(x => new { x.Status, x.NextRunAtUtc });
        builder.HasIndex(x => new { x.TenantId, x.Status });
    }
}