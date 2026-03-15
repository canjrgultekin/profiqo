// Path: backend/src/Profiqo.Infrastructure/Integrations/Hepsiburada/HepsiburadaSyncStore.cs
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Profiqo.Application.Abstractions.Integrations.Hepsiburada;
using Profiqo.Domain.Common.Ids;
using Profiqo.Domain.Common.Types;
using Profiqo.Domain.Customers;
using Profiqo.Domain.Integrations;
using Profiqo.Domain.Orders;
using Profiqo.Infrastructure.Persistence;

namespace Profiqo.Infrastructure.Integrations.Hepsiburada;

public sealed class HepsiburadaSyncStore : IHepsiburadaSyncStore
{
    private readonly ProfiqoDbContext _db;

    public HepsiburadaSyncStore(ProfiqoDbContext db)
    {
        _db = db;
    }

    public async Task UpsertOrderAsync(TenantId tenantId, HepsiburadaOrderUpsert model, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        // Müşteri email/phone normalize + hash
        var emailNorm = NormalizeEmail(model.CustomerEmail);
        var phoneNorm = NormalizePhone(model.CustomerPhone);

        var emailHash = string.IsNullOrWhiteSpace(emailNorm) ? null : Sha256Hex(emailNorm);
        var phoneHash = string.IsNullOrWhiteSpace(phoneNorm) ? null : Sha256Hex(phoneNorm);

        var customer = await ResolveOrCreateCustomerAsync(tenantId, emailHash, phoneHash, now, ct);

        // İsim bilgisini set et
        if (!string.IsNullOrWhiteSpace(model.CustomerName))
        {
            var nameParts = model.CustomerName.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            var firstName = nameParts.Length > 0 ? nameParts[0] : null;
            var lastName = nameParts.Length > 1 ? nameParts[1] : null;
            customer.SetName(firstName, lastName, now);
        }

        // Deterministik providerOrderId: orderNumber -> payload hash
        var providerOrderId = (model.OrderNumber ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(providerOrderId))
            providerOrderId = Sha256Hex(model.PayloadJson ?? string.Empty);

        var channel = SalesChannel.Hepsiburada;

        var shipJson = JsonSerializer.Serialize(new
        {
            addressDetail = model.ShippingAddress?.AddressDetail,
            city = model.ShippingAddress?.City,
            town = model.ShippingAddress?.Town,
            district = model.ShippingAddress?.District,
            country = model.ShippingAddress?.CountryCode,
            postalCode = model.ShippingAddress?.PostalCode,
            phone = model.ShippingAddress?.Phone,
            email = model.ShippingAddress?.Email,
            fullName = model.ShippingAddress?.FullName
        });

        var billJson = JsonSerializer.Serialize(new
        {
            addressDetail = model.BillingAddress?.AddressDetail,
            city = model.BillingAddress?.City,
            town = model.BillingAddress?.Town,
            district = model.BillingAddress?.District,
            country = model.BillingAddress?.CountryCode,
            postalCode = model.BillingAddress?.PostalCode,
            phone = model.BillingAddress?.Phone,
            email = model.BillingAddress?.Email,
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

            await _db.SaveChangesAsync(ct);
            return;
        }

        var currency = NormalizeCurrency(model.CurrencyCode);
        var totalAmount = new Money(model.TotalPrice, new CurrencyCode(currency));

        var lines = (model.Lines ?? Array.Empty<HepsiburadaOrderLineUpsert>())
            .Select(l =>
            {
                var lc = NormalizeCurrency(l.CurrencyCode);
                var qty = l.Quantity <= 0 ? 1 : l.Quantity;

                var unitMoney = new Money(l.UnitPrice, new CurrencyCode(lc));
                var discountMoney = new Money(l.Discount, new CurrencyCode(lc));

                var sku = string.IsNullOrWhiteSpace(l.Sku) ? "NA" : l.Sku.Trim();
                var name = string.IsNullOrWhiteSpace(l.ProductName) ? "unknown" : l.ProductName.Trim();

                var barcode = string.IsNullOrWhiteSpace(l.MerchantSku) ? null : l.MerchantSku.Trim();
                var statusName = string.IsNullOrWhiteSpace(l.OrderLineItemStatusName) ? null : l.OrderLineItemStatusName.Trim();

                return new OrderLine(
                    sku: sku,
                    productName: name,
                    quantity: qty,
                    unitPrice: unitMoney,
                    productCategory: null,
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
                customer.AddOrTouchIdentity(CustomerIdentity.Create(tenantId, IdentityType.Email, new IdentityHash(emailHash), null, ProviderType.Hepsiburada, null, nowUtc), nowUtc);

            if (!string.IsNullOrWhiteSpace(phoneHash))
                customer.AddOrTouchIdentity(CustomerIdentity.Create(tenantId, IdentityType.Phone, new IdentityHash(phoneHash), null, ProviderType.Hepsiburada, null, nowUtc), nowUtc);

            await _db.Customers.AddAsync(customer, ct);
            await _db.SaveChangesAsync(ct);
            return customer;
        }

        if (!string.IsNullOrWhiteSpace(emailHash))
            customer.AddOrTouchIdentity(CustomerIdentity.Create(tenantId, IdentityType.Email, new IdentityHash(emailHash), null, ProviderType.Hepsiburada, null, nowUtc), nowUtc);

        if (!string.IsNullOrWhiteSpace(phoneHash))
            customer.AddOrTouchIdentity(CustomerIdentity.Create(tenantId, IdentityType.Phone, new IdentityHash(phoneHash), null, ProviderType.Hepsiburada, null, nowUtc), nowUtc);

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