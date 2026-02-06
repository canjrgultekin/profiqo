using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Profiqo.Application.Abstractions.Persistence.WhatsappAutomation;
using Profiqo.Infrastructure.Persistence.Entities;

namespace Profiqo.Infrastructure.Persistence.Configurations;

internal static class WhatsappAutomationConfigurations
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WhatsappRuleRow>(ConfigureRules);
        modelBuilder.Entity<WhatsappJobRow>(ConfigureJobs);
        modelBuilder.Entity<WhatsappOrderEventRow>(ConfigureOrderEvents);
        modelBuilder.Entity<WhatsappDispatchQueueRow>(ConfigureDispatch);
    }


    private static void ConfigureRules(EntityTypeBuilder<WhatsappRuleRow> b)
    {
        b.ToTable("whatsapp_rules");
        b.HasKey(x => x.Id);

        b.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        b.Property(x => x.Name).HasColumnName("name").HasMaxLength(256).IsRequired();

        b.Property(x => x.Mode).HasColumnName("mode").HasConversion<short>().IsRequired();
        b.Property(x => x.DailyLimit).HasColumnName("daily_limit").IsRequired();
        b.Property(x => x.Timezone).HasColumnName("timezone").HasMaxLength(64).IsRequired();

        b.Property(x => x.DailyTime1).HasColumnName("daily_time1");
        b.Property(x => x.DailyTime2).HasColumnName("daily_time2");
        b.Property(x => x.DailyDelay2Minutes).HasColumnName("daily_delay2_minutes");

        b.Property(x => x.OrderDelay1Minutes).HasColumnName("order_delay1_minutes");
        b.Property(x => x.OrderDelay2Minutes).HasColumnName("order_delay2_minutes");

        b.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        b.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        b.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();

        b.HasIndex(x => new { x.TenantId, x.IsActive }).HasDatabaseName("ix_whatsapp_rules_tenant_active");
    }

    private static void ConfigureJobs(EntityTypeBuilder<WhatsappJobRow> b)
    {
        b.ToTable("whatsapp_jobs");
        b.HasKey(x => x.Id);

        b.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        b.Property(x => x.Name).HasColumnName("name").HasMaxLength(256).IsRequired();

        b.Property(x => x.RuleId).HasColumnName("rule_id").IsRequired();

        // 🔥 kritik fix
        b.Property(x => x.Template1Id).HasColumnName("template1_id").IsRequired();
        b.Property(x => x.Template2Id).HasColumnName("template2_id");

        b.Property(x => x.TargetsJson).HasColumnName("targets_json").HasColumnType("jsonb").IsRequired();
        b.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();

        b.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        b.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();

        b.HasIndex(x => new { x.TenantId, x.IsActive }).HasDatabaseName("ix_whatsapp_jobs_tenant_active");
        b.HasIndex(x => x.RuleId).HasDatabaseName("ix_whatsapp_jobs_rule");
    }

    private static void ConfigureOrderEvents(EntityTypeBuilder<WhatsappOrderEventRow> b)
    {
        b.ToTable("whatsapp_order_events");
        b.HasKey(x => x.Id);

        b.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        b.Property(x => x.OrderId).HasColumnName("order_id").HasMaxLength(128).IsRequired();
        b.Property(x => x.CustomerId).HasColumnName("customer_id").IsRequired();
        b.Property(x => x.ToE164).HasColumnName("to_e164").HasMaxLength(32).IsRequired();
        b.Property(x => x.OccurredAtUtc).HasColumnName("occurred_at_utc").IsRequired();
        b.Property(x => x.ProcessedAtUtc).HasColumnName("processed_at_utc");

        b.HasIndex(x => new { x.TenantId, x.OrderId }).IsUnique()
            .HasDatabaseName("ux_whatsapp_order_events_tenant_order");
        b.HasIndex(x => x.ProcessedAtUtc).HasDatabaseName("ix_whatsapp_order_events_unprocessed");
    }

    private static void ConfigureDispatch(EntityTypeBuilder<WhatsappDispatchQueueRow> b)
    {
        b.ToTable("whatsapp_dispatch_queue");
        b.HasKey(x => x.Id);

        b.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        b.Property(x => x.JobId).HasColumnName("job_id").IsRequired();
        b.Property(x => x.RuleId).HasColumnName("rule_id").IsRequired();
        b.Property(x => x.CustomerId).HasColumnName("customer_id").IsRequired();
        b.Property(x => x.ToE164).HasColumnName("to_e164").HasMaxLength(32).IsRequired();
        b.Property(x => x.MessageNo).HasColumnName("message_no").IsRequired();
        b.Property(x => x.TemplateId).HasColumnName("template_id").IsRequired();

        b.Property(x => x.PlannedAtUtc).HasColumnName("planned_at_utc").IsRequired();
        b.Property(x => x.LocalDate).HasColumnName("local_date").IsRequired();

        b.Property(x => x.Status).HasColumnName("status").HasConversion<short>().IsRequired();
        b.Property(x => x.AttemptCount).HasColumnName("attempt_count").IsRequired();
        b.Property(x => x.NextAttemptAtUtc).HasColumnName("next_attempt_at_utc").IsRequired();

        b.Property(x => x.LockedBy).HasColumnName("locked_by").HasMaxLength(200);
        b.Property(x => x.LockedAtUtc).HasColumnName("locked_at_utc");

        b.Property(x => x.PayloadJson).HasColumnName("payload_json").HasColumnType("jsonb").IsRequired();

        b.Property(x => x.SentAtUtc).HasColumnName("sent_at_utc");
        b.Property(x => x.LastError).HasColumnName("last_error").HasMaxLength(8000);

        b.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        b.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
        b.Property(x => x.IsSimulated).HasColumnName("is_simulated").IsRequired();

        b.HasIndex(x => new { x.Status, x.NextAttemptAtUtc }).HasDatabaseName("ix_whatsapp_dispatch_status_next");
        b.HasIndex(x => new { x.TenantId, x.JobId }).HasDatabaseName("ix_whatsapp_dispatch_tenant_job");
        b.HasIndex(x => new
            {
                x.TenantId,
                x.JobId,
                x.CustomerId,
                x.LocalDate,
                x.MessageNo
            })
            .IsUnique()
            .HasDatabaseName("ux_whatsapp_dispatch_dedupe");
    }
}
