using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Profiqo.Domain.Common.Ids;
using Profiqo.Infrastructure.Persistence.Entities;
using Profiqo.Infrastructure.Persistence.Converters;

namespace Profiqo.Infrastructure.Persistence.Configurations;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId)
            .HasConversion(new StronglyTypedIdConverter<TenantId>())
            .IsRequired();

        builder.Property(x => x.MessageType).HasMaxLength(400).IsRequired();
        builder.Property(x => x.PayloadJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.HeadersJson).HasColumnType("jsonb").IsRequired();

        builder.Property(x => x.Status).IsRequired();
        builder.Property(x => x.Attempts).IsRequired();
        builder.Property(x => x.NextAttemptAtUtc);
        builder.Property(x => x.LastError).HasColumnType("text");

        builder.Property(x => x.OccurredAtUtc).IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsRequired();

        builder.HasIndex(x => new { x.Status, x.NextAttemptAtUtc });
        builder.HasIndex(x => new { x.TenantId, x.OccurredAtUtc });
    }
}

internal sealed class InboxMessageConfiguration : IEntityTypeConfiguration<InboxMessage>
{
    public void Configure(EntityTypeBuilder<InboxMessage> builder)
    {
        builder.ToTable("inbox_messages");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId)
            .HasConversion(new StronglyTypedIdConverter<TenantId>())
            .IsRequired();

        builder.Property(x => x.ConsumerName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.MessageId).HasMaxLength(300).IsRequired();

        builder.Property(x => x.ProcessedAtUtc).IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.ConsumerName, x.MessageId }).IsUnique();
    }
}

internal sealed class IngestionEventConfiguration : IEntityTypeConfiguration<IngestionEvent>
{
    public void Configure(EntityTypeBuilder<IngestionEvent> builder)
    {
        builder.ToTable("ingestion_events");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId)
            .HasConversion(new StronglyTypedIdConverter<TenantId>())
            .IsRequired();

        builder.Property(x => x.ProviderType).HasConversion<short>().IsRequired();
        builder.Property(x => x.EventType).HasMaxLength(120).IsRequired();

        builder.Property(x => x.ProviderEventId).HasMaxLength(220).IsRequired();

        builder.Property(x => x.OccurredAtUtc).IsRequired();
        builder.Property(x => x.ReceivedAtUtc).IsRequired();

        builder.Property(x => x.SignatureValid).IsRequired();

        builder.Property(x => x.PayloadJson).HasColumnType("jsonb").IsRequired();

        builder.Property(x => x.ProcessingStatus).IsRequired();
        builder.Property(x => x.Attempts).IsRequired();
        builder.Property(x => x.NextAttemptAtUtc);
        builder.Property(x => x.LastError).HasColumnType("text");

        builder.Property(x => x.CreatedAtUtc).IsRequired();

        builder.HasIndex(x => new { x.ProcessingStatus, x.NextAttemptAtUtc });
        builder.HasIndex(x => new { x.TenantId, x.ProviderType, x.ProviderEventId }).IsUnique();
    }
}

internal sealed class IntegrationCursorConfiguration : IEntityTypeConfiguration<IntegrationCursor>
{
    public void Configure(EntityTypeBuilder<IntegrationCursor> builder)
    {
        builder.ToTable("integration_cursors");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId)
            .HasConversion(new StronglyTypedIdConverter<TenantId>())
            .IsRequired();

        builder.Property(x => x.ProviderConnectionId)
            .HasConversion(new StronglyTypedIdConverter<ProviderConnectionId>())
            .IsRequired();

        builder.Property(x => x.CursorKey).HasMaxLength(200).IsRequired();
        builder.Property(x => x.CursorValue).HasMaxLength(600).IsRequired();

        builder.Property(x => x.UpdatedAtUtc).IsRequired().IsConcurrencyToken();

        builder.HasIndex(x => new { x.TenantId, x.ProviderConnectionId, x.CursorKey }).IsUnique();
    }
}
