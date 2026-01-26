using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Profiqo.Domain.Common.Ids;
using Profiqo.Domain.Customers;
using Profiqo.Domain.Orders;
using Profiqo.Infrastructure.Persistence.Converters;

namespace Profiqo.Infrastructure.Persistence.Configurations;

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasConversion(new StronglyTypedIdConverter<OrderId>())
            .ValueGeneratedNever();

        builder.Property(x => x.TenantId)
            .HasConversion(new StronglyTypedIdConverter<TenantId>())
            .IsRequired();

        builder.Property(x => x.CustomerId)
            .HasConversion(new StronglyTypedIdConverter<CustomerId>())
            .IsRequired();

        builder.Property(x => x.Channel).HasConversion<short>().IsRequired();
        builder.Property(x => x.Status).HasConversion<short>().IsRequired();

        builder.Property(x => x.ProviderOrderId).HasMaxLength(200);

        builder.HasIndex(x => new { x.TenantId, x.Channel, x.ProviderOrderId }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.CustomerId, x.PlacedAtUtc });

        builder.Property(x => x.PlacedAtUtc).IsRequired();
        builder.Property(x => x.CompletedAtUtc);

        // IMPORTANT: CostBreakdown is domain-only, persisted as JSON string.
        builder.Ignore(x => x.CostBreakdown);

        builder.OwnsOne(x => x.TotalAmount, m =>
        {
            m.Property(p => p.Amount).HasColumnName("total_amount").HasColumnType("numeric(19,4)").IsRequired();
            m.OwnsOne(p => p.Currency, c =>
            {
                c.Property(x => x.Value).HasColumnName("total_currency").HasMaxLength(3).IsRequired();
            });
        });

        builder.OwnsOne(x => x.NetProfit, m =>
        {
            m.Property(p => p.Amount).HasColumnName("net_profit").HasColumnType("numeric(19,4)").IsRequired();
            m.OwnsOne(p => p.Currency, c =>
            {
                c.Property(x => x.Value).HasColumnName("net_profit_currency").HasMaxLength(3).IsRequired();
            });
        });

        builder.Property(x => x.CostBreakdownJson)
            .HasColumnName("cost_breakdown_json")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Navigation(x => x.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(x => x.Lines, l =>
        {
            l.ToTable("order_lines");
            l.WithOwner().HasForeignKey("order_id");

            l.Property<int>("line_id");
            l.HasKey("order_id", "line_id");

            l.Property(p => p.Sku).HasMaxLength(128).HasColumnName("sku").IsRequired();
            l.Property(p => p.ProductName).HasMaxLength(300).HasColumnName("product_name").IsRequired();
            l.Property(p => p.Quantity).HasColumnName("quantity").IsRequired();

            l.OwnsOne(p => p.UnitPrice, mp =>
            {
                mp.Property(x => x.Amount).HasColumnName("unit_price").HasColumnType("numeric(19,4)").IsRequired();
                mp.OwnsOne(x => x.Currency, c =>
                {
                    c.Property(v => v.Value).HasColumnName("unit_currency").HasMaxLength(3).IsRequired();
                });
            });

            l.OwnsOne(p => p.LineTotal, mp =>
            {
                mp.Property(x => x.Amount).HasColumnName("line_total").HasColumnType("numeric(19,4)").IsRequired();
                mp.OwnsOne(x => x.Currency, c =>
                {
                    c.Property(v => v.Value).HasColumnName("line_total_currency").HasMaxLength(3).IsRequired();
                });
            });

            l.HasIndex("order_id");
        });

        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).IsRequired().IsConcurrencyToken();
    }
}
