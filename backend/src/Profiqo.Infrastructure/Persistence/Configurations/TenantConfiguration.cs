using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Profiqo.Domain.Common.Ids;
using Profiqo.Domain.Tenants;
using Profiqo.Infrastructure.Persistence.Converters;

namespace Profiqo.Infrastructure.Persistence.Configurations;

internal sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenants");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasConversion(new StronglyTypedIdConverter<TenantId>());

        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Slug).HasMaxLength(80).IsRequired();

        builder.HasIndex(x => x.Slug).IsUnique();

        builder.Property(x => x.Status).HasConversion<short>().IsRequired();

        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).IsRequired().IsConcurrencyToken();
    }
}