using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Profiqo.Domain.Automation;
using Profiqo.Domain.Common.Ids;
using Profiqo.Infrastructure.Persistence.Converters;

namespace Profiqo.Infrastructure.Persistence.Configurations;

internal sealed class AutomationRuleConfiguration : IEntityTypeConfiguration<AutomationRule>
{
    public void Configure(EntityTypeBuilder<AutomationRule> builder)
    {
        builder.ToTable("automation_rules");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasConversion(new StronglyTypedIdConverter<AutomationRuleId>())
            .ValueGeneratedNever();

        builder.Property(x => x.TenantId)
            .HasConversion(new StronglyTypedIdConverter<TenantId>())
            .IsRequired();

        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.Status).HasConversion<short>().IsRequired();

        builder.OwnsOne(x => x.Trigger, b =>
        {
            b.Property(p => p.Type).HasConversion<short>().HasColumnName("trigger_type").IsRequired();
            b.Property(p => p.EventType).HasMaxLength(100).HasColumnName("trigger_event_type");
            b.Property(p => p.CronExpression).HasMaxLength(200).HasColumnName("trigger_cron");
            b.Property(p => p.ScoreField).HasMaxLength(200).HasColumnName("trigger_score_field");
            b.Property(p => p.ScoreOperator).HasConversion<short?>().HasColumnName("trigger_score_operator");
            b.Property(p => p.ScoreValueJson).HasColumnType("jsonb").HasColumnName("trigger_score_value_json");
        });

        builder.OwnsOne(x => x.Delay, b =>
        {
            b.Property(p => p.Value).HasColumnName("delay_value").IsRequired();
            b.Property(p => p.Unit).HasConversion<short>().HasColumnName("delay_unit").IsRequired();
        });

        builder.OwnsOne(x => x.Limits, b =>
        {
            b.Property(p => p.MaxPerCustomerPerDay).HasColumnName("limit_max_per_day").IsRequired();
            b.Property(p => p.MaxPerCustomerTotal).HasColumnName("limit_max_total").IsRequired();
            b.Property(p => p.CooldownHours).HasColumnName("limit_cooldown_hours").IsRequired();
        });

        builder.OwnsOne(x => x.Goal, b =>
        {
            b.Property(p => p.Type).HasConversion<short>().HasColumnName("goal_type").IsRequired();
            b.Property(p => p.AttributionWindowDays).HasColumnName("goal_window_days").IsRequired();
        });

        builder.OwnsMany(x => x.Conditions, b =>
        {
            b.ToTable("automation_rule_conditions");
            b.WithOwner().HasForeignKey("automation_rule_id");

            b.Property<int>("id");
            b.HasKey("automation_rule_id", "id");

            b.Property(p => p.Field).HasMaxLength(200).HasColumnName("field").IsRequired();
            b.Property(p => p.Operator).HasConversion<short>().HasColumnName("op").IsRequired();
            b.Property(p => p.ValueJson).HasColumnType("jsonb").HasColumnName("value_json").IsRequired();

            b.HasIndex("automation_rule_id");
        });

        builder.OwnsMany(x => x.Actions, b =>
        {
            b.ToTable("automation_rule_actions");
            b.WithOwner().HasForeignKey("automation_rule_id");

            b.Property<int>("id");
            b.HasKey("automation_rule_id", "id");

            b.Property(p => p.Type).HasConversion<short>().HasColumnName("type").IsRequired();
            b.Property(p => p.Channel).HasConversion<short?>().HasColumnName("channel");

            b.Property(p => p.TemplateId)
                .HasConversion(new StronglyTypedIdConverter<MessageTemplateId>())
                .HasColumnName("template_id");

            // IMPORTANT: EF cannot map IReadOnlyDictionary<string,string>
            // We persist only JSON, and ignore the dictionary property.
            b.Ignore(p => p.Personalization);

            b.Property(p => p.PersonalizationJson)
                .HasColumnName("personalization_json")
                .HasColumnType("jsonb");

            b.Property(p => p.Tag).HasMaxLength(80).HasColumnName("tag");
            b.Property(p => p.SegmentId).HasMaxLength(80).HasColumnName("segment_id");
            b.Property(p => p.TaskAssignee).HasMaxLength(200).HasColumnName("task_assignee");
            b.Property(p => p.TaskDescription).HasMaxLength(2000).HasColumnName("task_description");

            b.HasIndex("automation_rule_id");
        });

        builder.HasIndex(x => new { x.TenantId, x.Status });

        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).IsRequired().IsConcurrencyToken();
    }
}
