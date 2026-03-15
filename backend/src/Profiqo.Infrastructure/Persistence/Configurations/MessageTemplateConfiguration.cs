using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Profiqo.Domain.Common.Ids;
using Profiqo.Domain.Messaging;
using Profiqo.Infrastructure.Persistence.Converters;

namespace Profiqo.Infrastructure.Persistence.Configurations;

internal sealed class MessageTemplateConfiguration : IEntityTypeConfiguration<MessageTemplate>
{
    public void Configure(EntityTypeBuilder<MessageTemplate> builder)
    {
        builder.ToTable("message_templates");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasConversion(new StronglyTypedIdConverter<MessageTemplateId>())
            .ValueGeneratedNever();

        builder.Property(x => x.TenantId)
            .HasConversion(new StronglyTypedIdConverter<TenantId>())
            .IsRequired();

        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Channel).HasConversion<short>().IsRequired();
        builder.Property(x => x.Language).HasMaxLength(16).IsRequired();

        builder.Property(x => x.Body).HasColumnType("text").IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();

        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).IsRequired().IsConcurrencyToken();
    }
}