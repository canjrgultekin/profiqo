using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Profiqo.Infrastructure.Persistence.Entities;

namespace Profiqo.Infrastructure.Persistence.Configurations;

internal sealed class SyncAutomationRuleConnectionConfiguration : IEntityTypeConfiguration<SyncAutomationRuleConnection>
{
    public void Configure(EntityTypeBuilder<SyncAutomationRuleConnection> builder)
    {
        builder.ToTable("sync_automation_rule_connections");
        builder.HasKey(x => new { x.TenantId, x.RuleId, x.ConnectionId });

        builder.Property(x => x.TenantId).HasColumnName("tenant_id");
        builder.Property(x => x.RuleId).HasColumnName("rule_id");
        builder.Property(x => x.ConnectionId).HasColumnName("connection_id");
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");

        builder.HasIndex(x => new { x.TenantId, x.RuleId });
    }
}