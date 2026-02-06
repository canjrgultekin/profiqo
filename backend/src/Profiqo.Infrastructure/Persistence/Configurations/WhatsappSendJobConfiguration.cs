using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Profiqo.Application.Abstractions.Persistence.Whatsapp;
using Profiqo.Infrastructure.Persistence.Entities;

namespace Profiqo.Infrastructure.Persistence.Configurations;

internal sealed class WhatsappSendJobConfiguration : IEntityTypeConfiguration<WhatsappSendJob>
{
    public void Configure(EntityTypeBuilder<WhatsappSendJob> builder)
    {
        builder.ToTable("whatsapp_send_jobs");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.ConnectionId).IsRequired();

        builder.Property(x => x.Status).HasConversion<short>().IsRequired();
        builder.Property(x => x.AttemptCount).IsRequired();
        builder.Property(x => x.NextAttemptAtUtc).IsRequired();

        builder.Property(x => x.PayloadJson).HasColumnType("jsonb").IsRequired();

        builder.Property(x => x.LockedBy).HasMaxLength(200);
        builder.Property(x => x.LastError).HasMaxLength(8000);

        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).IsRequired().IsConcurrencyToken();

        builder.HasIndex(x => new { x.Status, x.NextAttemptAtUtc });
        builder.HasIndex(x => new { x.TenantId, x.ConnectionId, x.Status });
        builder.HasIndex(x => x.LockedAtUtc);
    }
}