using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Profiqo.Infrastructure.Persistence.Entities;

namespace Profiqo.Infrastructure.Persistence.Configurations;

internal sealed class RawEventConfiguration : IEntityTypeConfiguration<RawEvent>
{
    public void Configure(EntityTypeBuilder<RawEvent> builder)
    {
        builder.ToTable("raw_events");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.EventType).HasColumnName("event_type").HasColumnType("text").IsRequired();
        builder.Property(x => x.ExternalId).HasColumnName("external_id").HasColumnType("text").IsRequired();

        builder.Property(x => x.ProviderType).HasColumnName("provider_type").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();

        builder.Property(x => x.OccurredAtUtc).HasColumnName("occurred_at_utc").IsRequired();
        builder.Property(x => x.PayloadJson).HasColumnName("payload_json").HasColumnType("jsonb").IsRequired();

        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.ProviderType, x.EventType, x.ExternalId }).IsUnique()
            .HasDatabaseName("ux_raw_events_tenant_provider_type_external");

        builder.HasIndex(x => new { x.TenantId, x.OccurredAtUtc }).HasDatabaseName("ix_raw_events_tenant_occurred");
    }
}