using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Profiqo.Infrastructure.Persistence.Entities;

namespace Profiqo.Infrastructure.Persistence.Configurations;

public sealed class WebEventConfiguration : IEntityTypeConfiguration<WebEvent>
{
    public void Configure(EntityTypeBuilder<WebEvent> builder)
    {
        builder.ToTable("web_events");

        // Composite PK — ileride tenant bazlı partitioning için
        builder.HasKey(x => new { x.TenantId, x.Id });

        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.EventType).HasColumnName("event_type").HasMaxLength(50).IsRequired();
        builder.Property(x => x.DeviceIdHash).HasColumnName("device_id_hash").HasMaxLength(128).IsRequired();
        builder.Property(x => x.SessionId).HasColumnName("session_id").HasMaxLength(64);
        builder.Property(x => x.CustomerId).HasColumnName("customer_id");
        builder.Property(x => x.ClientIp).HasColumnName("client_ip").HasMaxLength(45);
        builder.Property(x => x.PageUrl).HasColumnName("page_url").HasMaxLength(2048);
        builder.Property(x => x.PagePath).HasColumnName("page_path").HasMaxLength(512);
        builder.Property(x => x.PageReferrer).HasColumnName("page_referrer").HasMaxLength(2048);
        builder.Property(x => x.PageTitle).HasColumnName("page_title").HasMaxLength(512);
        builder.Property(x => x.UserAgent).HasColumnName("user_agent").HasMaxLength(512);
        builder.Property(x => x.EventDataJson).HasColumnName("event_data").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.OccurredAtUtc).HasColumnName("occurred_at_utc").IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();

        // Indexes
        builder.HasIndex(x => new { x.TenantId, x.OccurredAtUtc })
            .HasDatabaseName("ix_web_events_tenant_occurred")
            .IsDescending(false, true);

        builder.HasIndex(x => new { x.TenantId, x.DeviceIdHash, x.OccurredAtUtc })
            .HasDatabaseName("ix_web_events_device_time");

        builder.HasIndex(x => new { x.TenantId, x.EventType, x.OccurredAtUtc })
            .HasDatabaseName("ix_web_events_type_time");
    }
}
