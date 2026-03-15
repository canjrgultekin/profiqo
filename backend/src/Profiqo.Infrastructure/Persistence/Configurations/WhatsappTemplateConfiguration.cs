using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Profiqo.Domain.Common.Ids;
using Profiqo.Domain.Integrations.Whatsapp;
using Profiqo.Infrastructure.Persistence.Converters;

namespace Profiqo.Infrastructure.Persistence.Configurations;

internal sealed class WhatsappTemplateConfiguration : IEntityTypeConfiguration<WhatsappTemplate>
{
    public void Configure(EntityTypeBuilder<WhatsappTemplate> builder)
    {
        builder.ToTable("whatsapp_templates");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasConversion(new StronglyTypedIdConverter<WhatsappTemplateId>())
            .ValueGeneratedNever();

        builder.Property(x => x.TenantId)
            .HasConversion(new StronglyTypedIdConverter<TenantId>())
            .IsRequired();

        builder.Property(x => x.ConnectionId)
            .HasConversion(new StronglyTypedIdConverter<ProviderConnectionId>())
            .IsRequired();

        builder.Property(x => x.Name).HasMaxLength(512).IsRequired();
        builder.Property(x => x.Language).HasMaxLength(16).IsRequired();
        builder.Property(x => x.Category).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();

        builder.Property(x => x.ComponentsJson).HasColumnType("jsonb").IsRequired();

        builder.Property(x => x.MetaTemplateId).HasMaxLength(200);
        builder.Property(x => x.RejectionReason).HasMaxLength(2000);

        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).IsRequired().IsConcurrencyToken();

        builder.HasIndex(x => new { x.TenantId, x.ConnectionId, x.Name }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.ConnectionId, x.Status });
    }
}