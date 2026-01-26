using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Profiqo.Domain.Common.Ids;
using Profiqo.Domain.Users;
using Profiqo.Infrastructure.Persistence.Converters;

namespace Profiqo.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasConversion(new StronglyTypedIdConverter<UserId>())
            .ValueGeneratedNever();

        builder.Property(x => x.TenantId)
            .HasConversion(new StronglyTypedIdConverter<TenantId>())
            .IsRequired();

        // Email value object -> single column
        builder.Property(x => x.Email)
            .HasConversion(new EmailAddressConverter())
            .HasColumnName("email")
            .HasMaxLength(254)
            .IsRequired();

        builder.Property(x => x.PasswordHash)
            .HasColumnName("password_hash")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(x => x.DisplayName)
            .HasColumnName("display_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<short>()
            .HasColumnName("status")
            .IsRequired();

        // Roles: shadow jsonb (faz-1)
        builder.Property<string>("roles_json")
            .HasColumnName("roles_json")
            .HasColumnType("jsonb")
            .HasDefaultValue("[]")
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired().IsConcurrencyToken();

        builder.HasIndex(x => x.TenantId).HasDatabaseName("ix_users_tenant_id");

        // Kritik: artık string adıyla değil expression ile unique index
        builder.HasIndex(x => x.Email).IsUnique().HasDatabaseName("ux_users_email");
    }
}
