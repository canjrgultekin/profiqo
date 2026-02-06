// Path: backend/src/Profiqo.Infrastructure/Integrations/Ikas/IkasSyncStore.cs
using System.Security.Cryptography;
using System.Text;

using Microsoft.EntityFrameworkCore;

using Profiqo.Application.Abstractions.Integrations.Ikas;
using Profiqo.Application.Customers.IdentityResolution;
using Profiqo.Domain.Common.Ids;
using Profiqo.Domain.Common.Types;
using Profiqo.Domain.Customers;
using Profiqo.Domain.Integrations;
using Profiqo.Domain.Orders;
using Profiqo.Infrastructure.Persistence;
using Profiqo.Infrastructure.Persistence.Entities;

namespace Profiqo.Infrastructure.Integrations.Ikas;

public sealed class IkasSyncStore : IIkasSyncStore
{
    private readonly ProfiqoDbContext _db;
    private readonly IIdentityResolutionService _resolver;

    public IkasSyncStore(ProfiqoDbContext db, IIdentityResolutionService resolver)
    {
        _db = db;
        _resolver = resolver;
    }

    public async Task<CustomerId> UpsertCustomerAsync(TenantId tenantId, IkasCustomerUpsert model, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        var ids = new List<IdentityInput>();

        if (!string.IsNullOrWhiteSpace(model.EmailNormalized) && !string.IsNullOrWhiteSpace(model.EmailHashSha256))
            ids.Add(new IdentityInput(
                IdentityType.Email,
                model.EmailNormalized,
                new IdentityHash(model.EmailHashSha256),
                ProviderType.Ikas,
                model.ProviderCustomerId));

        if (!string.IsNullOrWhiteSpace(model.PhoneNormalized) && !string.IsNullOrWhiteSpace(model.PhoneHashSha256))
            ids.Add(new IdentityInput(
                IdentityType.Phone,
                model.PhoneNormalized,
                new IdentityHash(model.PhoneHashSha256),
                ProviderType.Ikas,
                model.ProviderCustomerId));

        // ProviderCustomerId identity (match order doesn't use it, ama provider map ve audit için faydalı)
        if (!string.IsNullOrWhiteSpace(model.ProviderCustomerId))
        {
            var pid = model.ProviderCustomerId.Trim();
            // type=ProviderCustomerId tenant içinde unique, collision olmasın diye provider prefix ekliyoruz
            var hash = Sha256Hex($"ikas:{pid}");
            ids.Add(new IdentityInput(
                IdentityType.ProviderCustomerId,
                pid,
                new IdentityHash(hash),
                ProviderType.Ikas,
                pid));
        }

        var customerId = await _resolver.ResolveOrCreateCustomerAsync(
            tenantId,
            model.FirstName,
            model.LastName,
            ids,
            now,
            ct);

        await _db.SaveChangesAsync(ct);
        return customerId;
    }

    public async Task<OrderId> UpsertOrderAsync(TenantId tenantId, IkasOrderUpsert model, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        // 1) Customer resolve (email/phone varsa canonical'a gider, yoksa yeni customer create eder)
        var ids = new List<IdentityInput>();

        if (!string.IsNullOrWhiteSpace(model.CustomerEmailNormalized) && !string.IsNullOrWhiteSpace(model.CustomerEmailHashSha256))
            ids.Add(new IdentityInput(
                IdentityType.Email,
                model.CustomerEmailNormalized,
                new IdentityHash(model.CustomerEmailHashSha256),
                ProviderType.Ikas,
                model.ProviderOrderId));

        if (!string.IsNullOrWhiteSpace(model.CustomerPhoneNormalized) && !string.IsNullOrWhiteSpace(model.CustomerPhoneHashSha256))
            ids.Add(new IdentityInput(
                IdentityType.Phone,
                model.CustomerPhoneNormalized,
                new IdentityHash(model.CustomerPhoneHashSha256),
                ProviderType.Ikas,
                model.ProviderOrderId));

        var customerId = await _resolver.ResolveOrCreateCustomerAsync(
            tenantId,
            firstName: null,
            lastName: null,
            identities: ids,
            nowUtc: now,
            ct: ct);

        var currency = new CurrencyCode(model.CurrencyCode);

        // 2) Order already exists? -> idempotent, ama provider status + address snapshot güncellenir
        var existing = await _db.Orders
            .FirstOrDefaultAsync(o =>
                o.TenantId == tenantId &&
                o.Channel == SalesChannel.Ikas &&
                o.ProviderOrderId == model.ProviderOrderId, ct);

        if (existing is not null)
        {
            // provider status update
            existing.SetProviderOrderStatus(model.OrderStatus, now);

            // address snapshots update
            _db.Entry(existing).Property("ShippingAddressJson").CurrentValue =
                string.IsNullOrWhiteSpace(model.ShippingAddressJson) ? null : model.ShippingAddressJson;

            _db.Entry(existing).Property("BillingAddressJson").CurrentValue =
                string.IsNullOrWhiteSpace(model.BillingAddressJson) ? null : model.BillingAddressJson;

            await _db.SaveChangesAsync(ct);
            return existing.Id;
        }

        // 3) Build lines with new fields (productCategory, barcode, discount, line status)
        var lines = new List<OrderLine>();

        if (model.Lines is not null && model.Lines.Count > 0)
        {
            foreach (var l in model.Lines)
            {
                var lineCurrency = new CurrencyCode(l.CurrencyCode);

                var sku = string.IsNullOrWhiteSpace(l.Sku)
                    ? (l.ProviderVariantId ?? l.ProviderProductId ?? "unknown")
                    : l.Sku!.Trim();

                var name = string.IsNullOrWhiteSpace(l.ProductName) ? "unknown" : l.ProductName.Trim();

                // unitPrice olarak finalPrice basıyoruz (line total unitPrice*qty)
                var unitPriceMoney = new Money(l.FinalPrice, lineCurrency);

                var discountMoney = new Money(l.Discount, lineCurrency);

                lines.Add(new OrderLine(
                    sku: sku,
                    productName: name,
                    quantity: l.Quantity <= 0 ? 1 : l.Quantity,
                    unitPrice: unitPriceMoney,
                    productCategory: string.IsNullOrWhiteSpace(l.ProductCategory) ? null : l.ProductCategory.Trim(),
                    barcode: string.IsNullOrWhiteSpace(l.Barcode) ? null : l.Barcode.Trim(),
                    discount: discountMoney,
                    orderLineItemStatusName: string.IsNullOrWhiteSpace(l.OrderLineItemStatusName) ? null : l.OrderLineItemStatusName.Trim()
                ));
            }
        }
        else
        {
            lines.Add(new OrderLine(
                sku: "ikas",
                productName: "ikas order",
                quantity: 1,
                unitPrice: new Money(model.TotalFinalPrice, currency),
                productCategory: null,
                barcode: null,
                discount: Money.Zero(currency),
                orderLineItemStatusName: null
            ));
        }

        var total = new Money(model.TotalFinalPrice, currency);

        var order = Order.Create(
            tenantId: tenantId,
            customerId: customerId,
            channel: SalesChannel.Ikas,
            providerOrderId: model.ProviderOrderId,
            placedAtUtc: model.PlacedAtUtc,
            lines: lines,
            totalAmount: total,
            nowUtc: now);

        // ✅ provider order status
        order.SetProviderOrderStatus(model.OrderStatus, now);

        await _db.Orders.AddAsync(order, ct);

        // ✅ shadow json columns
        _db.Entry(order).Property("ShippingAddressJson").CurrentValue =
            string.IsNullOrWhiteSpace(model.ShippingAddressJson) ? null : model.ShippingAddressJson;

        _db.Entry(order).Property("BillingAddressJson").CurrentValue =
            string.IsNullOrWhiteSpace(model.BillingAddressJson) ? null : model.BillingAddressJson;

        await _db.SaveChangesAsync(ct);

        return order.Id;
    }

    public async Task UpsertAbandonedCheckoutAsync(TenantId tenantId, ProviderConnectionId connectionId, IkasAbandonedCheckoutUpsert model, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        var providerType = (short)ProviderType.Ikas;

        var set = _db.Set<AbandonedCheckout>();
        var existing = await set.FirstOrDefaultAsync(x =>
            x.TenantId == tenantId.Value &&
            x.ProviderType == providerType &&
            x.ExternalId == model.ExternalId, ct);

        if (existing is null)
        {
            var created = new AbandonedCheckout(
                id: Guid.NewGuid(),
                tenantId: tenantId.Value,
                providerType: providerType,
                externalId: model.ExternalId,
                customerEmail: model.CustomerEmail,
                customerPhone: model.CustomerPhone,
                lastActivityDateMs: model.LastActivityDateMs,
                currencyCode: model.CurrencyCode,
                totalFinalPrice: model.TotalFinalPrice,
                status: model.Status,
                payloadJson: model.PayloadJson,
                nowUtc: now);

            await set.AddAsync(created, ct);
        }
        else
        {
            existing.Update(
                customerEmail: model.CustomerEmail,
                customerPhone: model.CustomerPhone,
                lastActivityDateMs: model.LastActivityDateMs,
                currencyCode: model.CurrencyCode,
                totalFinalPrice: model.TotalFinalPrice,
                status: model.Status,
                payloadJson: model.PayloadJson,
                nowUtc: now);
        }

        var evtSet = _db.Set<RawEvent>();
        var eventType = "cart_abandoned";
        var externalId = model.ExternalId;

        var rawExists = await evtSet.AsNoTracking().AnyAsync(x =>
            x.TenantId == tenantId.Value &&
            x.ProviderType == providerType &&
            x.EventType == eventType &&
            x.ExternalId == externalId, ct);

        if (!rawExists)
        {
            var occurred = DateTimeOffset.FromUnixTimeMilliseconds(model.LastActivityDateMs);
            var raw = new RawEvent(
                id: Guid.NewGuid(),
                tenantId: tenantId.Value,
                providerType: providerType,
                eventType: eventType,
                externalId: externalId,
                occurredAtUtc: occurred,
                payloadJson: model.PayloadJson,
                nowUtc: now);

            await evtSet.AddAsync(raw, ct);
        }

        await _db.SaveChangesAsync(ct);
    }

    private static string Sha256Hex(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}
