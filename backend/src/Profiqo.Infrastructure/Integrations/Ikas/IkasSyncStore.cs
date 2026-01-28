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
            ids.Add(new IdentityInput(IdentityType.Email, model.EmailNormalized, new IdentityHash(model.EmailHashSha256), ProviderType.Ikas, model.ProviderCustomerId));

        if (!string.IsNullOrWhiteSpace(model.PhoneNormalized) && !string.IsNullOrWhiteSpace(model.PhoneHashSha256))
            ids.Add(new IdentityInput(IdentityType.Phone, model.PhoneNormalized, new IdentityHash(model.PhoneHashSha256), ProviderType.Ikas, model.ProviderCustomerId));

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
        var exists = await _db.Orders.AsNoTracking()
            .AnyAsync(o => o.TenantId == tenantId && o.Channel == SalesChannel.Ikas && o.ProviderOrderId == model.ProviderOrderId, ct);

        if (exists)
        {
            var id = await _db.Orders.AsNoTracking()
                .Where(o => o.TenantId == tenantId && o.Channel == SalesChannel.Ikas && o.ProviderOrderId == model.ProviderOrderId)
                .Select(o => o.Id)
                .FirstAsync(ct);
            return id;
        }

        var now = DateTimeOffset.UtcNow;

        var ids = new List<IdentityInput>();

        if (!string.IsNullOrWhiteSpace(model.CustomerEmailNormalized) && !string.IsNullOrWhiteSpace(model.CustomerEmailHashSha256))
            ids.Add(new IdentityInput(IdentityType.Email, model.CustomerEmailNormalized, new IdentityHash(model.CustomerEmailHashSha256), ProviderType.Ikas, model.ProviderOrderId));

        if (!string.IsNullOrWhiteSpace(model.CustomerPhoneNormalized) && !string.IsNullOrWhiteSpace(model.CustomerPhoneHashSha256))
            ids.Add(new IdentityInput(IdentityType.Phone, model.CustomerPhoneNormalized, new IdentityHash(model.CustomerPhoneHashSha256), ProviderType.Ikas, model.ProviderOrderId));

        var customerId = ids.Count > 0
            ? await _resolver.ResolveOrCreateCustomerAsync(tenantId, null, null, ids, now, ct)
            : CustomerId.New();

        var currency = new CurrencyCode(model.CurrencyCode);

        // Create real order lines from Ikas orderLineItems
        var lines = new List<OrderLine>();

        if (model.Lines is not null && model.Lines.Count > 0)
        {
            foreach (var l in model.Lines)
            {
                var lineCurrency = new CurrencyCode(l.CurrencyCode);
                var lineTotal = new Money(l.FinalPrice * l.Quantity, lineCurrency);

                var sku = string.IsNullOrWhiteSpace(l.Sku) ? (l.ProviderVariantId ?? l.ProviderProductId ?? "unknown") : l.Sku!;
                var name = string.IsNullOrWhiteSpace(l.ProductName) ? "unknown" : l.ProductName;

                // IMPORTANT: This assumes OrderLine ctor: (sku, productName, quantity, lineTotal)
                lines.Add(new OrderLine(sku, name, l.Quantity, lineTotal));
            }
        }
        else
        {
            // fallback
            lines.Add(new OrderLine("ikas", "ikas order", 1, new Money(model.TotalFinalPrice, currency)));
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

        await _db.Orders.AddAsync(order, ct);
        await _db.SaveChangesAsync(ct);

        return order.Id;
    }

    public async Task UpsertAbandonedCheckoutAsync(TenantId tenantId, ProviderConnectionId connectionId, IkasAbandonedCheckoutUpsert model, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        var providerType = (short)ProviderType.Ikas;

        // 1) upsert abandoned_checkouts
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

        // 2) raw_events (dedupe by unique index)
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
}
