using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Profiqo.Domain.Common.Ids;
using Profiqo.Domain.Integrations;
using Profiqo.Infrastructure.Persistence.Converters;

namespace Profiqo.Infrastructure.Persistence.Configurations;

internal sealed class ProviderConnectionConfiguration : IEntityTypeConfiguration<ProviderConnection>
{
    public void Configure(EntityTypeBuilder<ProviderConnection> builder)
    {
        builder.ToTable("provider_connections");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasConversion(new StronglyTypedIdConverter<ProviderConnectionId>())
            .ValueGeneratedNever();

        builder.Property(x => x.TenantId)
            .HasConversion(new StronglyTypedIdConverter<TenantId>())
            .IsRequired();

        builder.Property(x => x.ProviderType).HasConversion<short>().IsRequired();
        builder.Property(x => x.Status).HasConversion<short>().IsRequired();

        builder.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ExternalAccountId).HasMaxLength(200);

        builder.Property(x => x.AccessTokenExpiresAtUtc).HasColumnName("access_token_expires_at_utc");

        builder.HasIndex(x => new { x.TenantId, x.ProviderType }).IsUnique();

        builder.OwnsOne(x => x.AccessToken, s =>
        {
            s.Property(p => p.CipherText).HasColumnName("access_token_ciphertext").HasColumnType("text").IsRequired();
            s.Property(p => p.KeyId).HasColumnName("access_token_key_id").HasMaxLength(128).IsRequired();
            s.Property(p => p.Algorithm).HasColumnName("access_token_algorithm").HasMaxLength(64).IsRequired();
        });

        builder.OwnsOne(x => x.RefreshToken, s =>
        {
            s.Property(p => p.CipherText).HasColumnName("refresh_token_ciphertext").HasColumnType("text");
            s.Property(p => p.KeyId).HasColumnName("refresh_token_key_id").HasMaxLength(128);
            s.Property(p => p.Algorithm).HasColumnName("refresh_token_algorithm").HasMaxLength(64);
        });

        builder.Navigation(x => x.RefreshToken).IsRequired(false);

        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).IsRequired().IsConcurrencyToken();
    }
}
