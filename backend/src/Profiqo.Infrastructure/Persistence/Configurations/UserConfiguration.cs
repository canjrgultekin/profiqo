using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

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

        var rolesConverter = new ValueConverter<List<UserRole>, string>(
            v => JsonSerializer.Serialize(v.Select(r => (short)r).ToArray(), (JsonSerializerOptions?)null),
            v => string.IsNullOrWhiteSpace(v)
                ? new List<UserRole>()
                : (JsonSerializer.Deserialize<short[]>(v, (JsonSerializerOptions?)null) ?? Array.Empty<short>())
                .Select(x => (UserRole)x)
                .ToList()
        );

        var rolesComparer = new ValueComparer<List<UserRole>>(
            (l1, l2) => l1.SequenceEqual(l2),
            l => l.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
            l => l.ToList());

        builder.Property<List<UserRole>>("_roles")
            .HasColumnName("roles_json")
            .HasColumnType("jsonb")
            .HasConversion(rolesConverter)
            .Metadata.SetValueComparer(rolesComparer);

        builder.Property<List<UserRole>>("_roles")
            .IsRequired()
            .HasDefaultValueSql("'[]'");

        builder.Ignore(x => x.Roles);

        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired().IsConcurrencyToken();

        builder.HasIndex(x => x.TenantId).HasDatabaseName("ix_users_tenant_id");
        builder.HasIndex(x => x.Email).IsUnique().HasDatabaseName("ux_users_email");
    }
}
