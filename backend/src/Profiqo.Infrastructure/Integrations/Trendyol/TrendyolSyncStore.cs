// Path: backend/src/Profiqo.Infrastructure/Integrations/Trendyol/TrendyolSyncStore.cs
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Profiqo.Application.Abstractions.Integrations.Trendyol;
using Profiqo.Domain.Common.Ids;
using Profiqo.Domain.Common.Types;
using Profiqo.Domain.Customers;
using Profiqo.Domain.Integrations;
using Profiqo.Domain.Orders;
using Profiqo.Infrastructure.Persistence;

namespace Profiqo.Infrastructure.Integrations.Trendyol;

public sealed class TrendyolSyncStore : ITrendyolSyncStore
{
    private readonly ProfiqoDbContext _db;

    public TrendyolSyncStore(ProfiqoDbContext db)
    {
        _db = db;
    }

    public async Task UpsertOrderAsync(TenantId tenantId, TrendyolOrderUpsert model, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        var emailNorm = NormalizeEmail(model.CustomerEmail);
        var phoneNorm = NormalizePhone(model.CustomerPhone);

        var emailHash = string.IsNullOrWhiteSpace(emailNorm) ? null : Sha256Hex(emailNorm);
        var phoneHash = string.IsNullOrWhiteSpace(phoneNorm) ? null : Sha256Hex(phoneNorm);

        var customer = await ResolveOrCreateCustomerAsync(tenantId, emailHash, phoneHash, now, ct);

        if (!string.IsNullOrWhiteSpace(model.CustomerFirstName) || !string.IsNullOrWhiteSpace(model.CustomerLastName))
            customer.SetName(model.CustomerFirstName, model.CustomerLastName, now);

        // Deterministik providerOrderId: shipmentPackageId -> orderNumber -> payload hash
        var providerOrderId = (model.ShipmentPackageId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(providerOrderId))
            providerOrderId = (model.OrderNumber ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(providerOrderId))
            providerOrderId = Sha256Hex(model.PayloadJson ?? string.Empty);

        var channel = SalesChannel.Trendyol;

        var shipJson = JsonSerializer.Serialize(new
        {
            addressLine1 = model.ShippingAddress?.Address1,
            addressLine2 = model.ShippingAddress?.Address2,
            city = model.ShippingAddress?.City,
            cityCode = model.ShippingAddress?.CityCode,
            district = model.ShippingAddress?.District,
            districtCode = model.ShippingAddress?.DistrictId,
            country = model.ShippingAddress?.CountryCode,
            postalCode = model.ShippingAddress?.PostalCode,
            phone = model.ShippingAddress?.Phone,
            fullName = model.ShippingAddress?.FullName
        });

        var billJson = JsonSerializer.Serialize(new
        {
            addressLine1 = model.BillingAddress?.Address1,
            addressLine2 = model.BillingAddress?.Address2,
            city = model.BillingAddress?.City,
            cityCode = model.BillingAddress?.CityCode,
            district = model.BillingAddress?.District,
            districtCode = model.BillingAddress?.DistrictId,
            country = model.BillingAddress?.CountryCode,
            postalCode = model.BillingAddress?.PostalCode,
            phone = model.BillingAddress?.Phone,
            fullName = model.BillingAddress?.FullName
        });

        var existing = await _db.Orders
            .FirstOrDefaultAsync(x =>
                x.TenantId == tenantId &&
                x.Channel == channel &&
                x.ProviderOrderId == providerOrderId, ct);

        if (existing is not null)
        {
            // Mükerrer yaratma yok, sadece güncelle.
            existing.SetProviderOrderStatus(model.OrderStatus, now);

            _db.Entry(existing).Property("ShippingAddressJson").CurrentValue = shipJson;
            _db.Entry(existing).Property("BillingAddressJson").CurrentValue = billJson;

            // İstersen payload snapshot da güncel kalsın (kolon varsa).
            // _db.Entry(existing).Property("PayloadJson").CurrentValue = model.PayloadJson;

            await _db.SaveChangesAsync(ct);
            return;
        }

        var currency = NormalizeCurrency(model.CurrencyCode);
        var totalAmount = new Money(model.TotalPrice, new CurrencyCode(currency));

        var lines = (model.Lines ?? Array.Empty<TrendyolOrderLineUpsert>())
            .Select(l =>
            {
                var lc = NormalizeCurrency(l.CurrencyCode);
                var qty = l.Quantity <= 0 ? 1 : l.Quantity;

                var unitMoney = new Money(l.UnitPrice, new CurrencyCode(lc));
                var discountMoney = new Money(l.Discount, new CurrencyCode(lc));

                var sku = string.IsNullOrWhiteSpace(l.Sku) ? "NA" : l.Sku.Trim();
                var name = string.IsNullOrWhiteSpace(l.ProductName) ? "unknown" : l.ProductName.Trim();

                var productCategory = string.IsNullOrWhiteSpace(l.ProductCategoryId) ? null : l.ProductCategoryId.Trim();
                var barcode = string.IsNullOrWhiteSpace(l.Barcode) ? null : l.Barcode.Trim();
                var statusName = string.IsNullOrWhiteSpace(l.OrderLineItemStatusName) ? null : l.OrderLineItemStatusName.Trim();

                return new OrderLine(
                    sku: sku,
                    productName: name,
                    quantity: qty,
                    unitPrice: unitMoney,
                    productCategory: productCategory,
                    brandName: null,
                    categoryNames: null,
                    barcode: barcode,
                    discount: discountMoney,
                    orderLineItemStatusName: statusName
                );
            })
            .ToList();

        if (lines.Count == 0)
            lines.Add(new OrderLine("NA", "unknown", 1, Money.Zero(new CurrencyCode(currency))));

        var created = Order.Create(
            tenantId: tenantId,
            customerId: customer.Id,
            channel: channel,
            providerOrderId: providerOrderId,
            placedAtUtc: model.OrderDateUtc,
            lines: lines,
            totalAmount: totalAmount,
            nowUtc: now);

        created.SetProviderOrderStatus(model.OrderStatus, now);

        await _db.Orders.AddAsync(created, ct);

        _db.Entry(created).Property("ShippingAddressJson").CurrentValue = shipJson;
        _db.Entry(created).Property("BillingAddressJson").CurrentValue = billJson;

        await _db.SaveChangesAsync(ct);
    }

    private async Task<Customer> ResolveOrCreateCustomerAsync(TenantId tenantId, string? emailHash, string? phoneHash, DateTimeOffset nowUtc, CancellationToken ct)
    {
        Customer? customer = null;

        if (!string.IsNullOrWhiteSpace(emailHash))
            customer = await FindCustomerByIdentityHashAsync(tenantId.Value, (short)IdentityType.Email, emailHash, ct);

        if (customer is null && !string.IsNullOrWhiteSpace(phoneHash))
            customer = await FindCustomerByIdentityHashAsync(tenantId.Value, (short)IdentityType.Phone, phoneHash, ct);

        if (customer is null)
        {
            customer = Customer.Create(tenantId, nowUtc);

            if (!string.IsNullOrWhiteSpace(emailHash))
                customer.AddOrTouchIdentity(CustomerIdentity.Create(tenantId, IdentityType.Email, new IdentityHash(emailHash), null, ProviderType.Trendyol, null, nowUtc), nowUtc);

            if (!string.IsNullOrWhiteSpace(phoneHash))
                customer.AddOrTouchIdentity(CustomerIdentity.Create(tenantId, IdentityType.Phone, new IdentityHash(phoneHash), null, ProviderType.Trendyol, null, nowUtc), nowUtc);

            await _db.Customers.AddAsync(customer, ct);
            await _db.SaveChangesAsync(ct);
            return customer;
        }

        // Bulunan müşteri tracked olmalı, yoksa alttaki mutasyonlar DB’ye yazmayabilir.
        if (!string.IsNullOrWhiteSpace(emailHash))
            customer.AddOrTouchIdentity(CustomerIdentity.Create(tenantId, IdentityType.Email, new IdentityHash(emailHash), null, ProviderType.Trendyol, null, nowUtc), nowUtc);

        if (!string.IsNullOrWhiteSpace(phoneHash))
            customer.AddOrTouchIdentity(CustomerIdentity.Create(tenantId, IdentityType.Phone, new IdentityHash(phoneHash), null, ProviderType.Trendyol, null, nowUtc), nowUtc);

        customer.SeenNow(nowUtc);
        await _db.SaveChangesAsync(ct);
        return customer;
    }

    private async Task<Customer?> FindCustomerByIdentityHashAsync(Guid tenantGuid, short identityType, string valueHash, CancellationToken ct)
    {
        const string sql = @"
SELECT c.*
FROM public.customers c
JOIN public.customer_identities i ON i.customer_id = c.id
WHERE c.tenant_id = {0} AND i.tenant_id = {0} AND i.type = {1} AND i.value_hash = {2}
LIMIT 1";

        // AsNoTracking kaldırıldı, aksi halde customer üstündeki değişiklikler persist olmayabilir.
        return await _db.Customers
            .FromSqlRaw(sql, tenantGuid, identityType, valueHash)
            .FirstOrDefaultAsync(ct);
    }

    private static string NormalizeEmail(string? email) => (email ?? "").Trim().ToLowerInvariant();

    private static string NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return "";
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("90")) return "+" + digits;
        if (digits.Length == 10) return "+90" + digits;
        return digits.Length > 0 ? "+" + digits : "";
    }

    private static string NormalizeCurrency(string? currency)
    {
        var c = (currency ?? "TRY").Trim().ToUpperInvariant();
        return c.Length == 3 ? c : "TRY";
    }

    private static string Sha256Hex(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}