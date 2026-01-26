using Microsoft.EntityFrameworkCore;

using Profiqo.Application.Abstractions.Crypto;
using Profiqo.Application.Abstractions.Integrations.Ikas;
using Profiqo.Domain.Common.Ids;
using Profiqo.Domain.Customers;
using Profiqo.Domain.Integrations;
using Profiqo.Domain.Orders;
using Profiqo.Domain.Common.Types;
using Profiqo.Infrastructure.Persistence;

namespace Profiqo.Infrastructure.Integrations.Ikas;

internal sealed class IkasSyncStore : IIkasSyncStore
{
    private readonly ProfiqoDbContext _db;
    private readonly ISecretProtector _secrets;

    public IkasSyncStore(ProfiqoDbContext db, ISecretProtector secrets)
    {
        _db = db;
        _secrets = secrets;
    }

    public async Task<CustomerId> UpsertCustomerAsync(TenantId tenantId, IkasCustomerUpsert model, CancellationToken ct)
    {
        // Try find by email hash then phone hash via owned identities
        Customer? existing = null;

        if (!string.IsNullOrWhiteSpace(model.EmailHashSha256))
        {
            var h = new IdentityHash(model.EmailHashSha256);
            existing = await _db.Customers
                .AsTracking()
                .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Identities.Any(i => i.Type == IdentityType.Email && i.ValueHash == h), ct);
        }

        if (existing is null && !string.IsNullOrWhiteSpace(model.PhoneHashSha256))
        {
            var h = new IdentityHash(model.PhoneHashSha256);
            existing = await _db.Customers
                .AsTracking()
                .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Identities.Any(i => i.Type == IdentityType.Phone && i.ValueHash == h), ct);
        }

        var now = DateTimeOffset.UtcNow;

        if (existing is null)
        {
            var created = Customer.Create(tenantId, now);
            created.SetName(model.FirstName, model.LastName, now);

            AddIdentityIfPresent(created, tenantId, model, now);

            await _db.Customers.AddAsync(created, ct);
            await _db.SaveChangesAsync(ct);

            return created.Id;
        }

        existing.SetName(model.FirstName, model.LastName, now);
        AddIdentityIfPresent(existing, tenantId, model, now);

        await _db.SaveChangesAsync(ct);

        return existing.Id;
    }

    public async Task<OrderId> UpsertOrderAsync(TenantId tenantId, IkasOrderUpsert model, CancellationToken ct)
    {
        // Idempotent: (TenantId, Channel=Ikas, ProviderOrderId) unique index should exist
        var existing = await _db.Orders
            .AsTracking()
            .FirstOrDefaultAsync(o => o.TenantId == tenantId && o.Channel == SalesChannel.Ikas && o.ProviderOrderId == model.ProviderOrderId, ct);

        if (existing is not null)
            return existing.Id;

        // Find/Upsert customer (best-effort)
        var custId = CustomerId.New();
        if (!string.IsNullOrWhiteSpace(model.CustomerEmailHashSha256) || !string.IsNullOrWhiteSpace(model.CustomerPhoneHashSha256))
        {
            var c = await UpsertCustomerAsync(tenantId,
                new IkasCustomerUpsert(
                    ProviderCustomerId: "ikas-order",
                    FirstName: null,
                    LastName: null,
                    EmailNormalized: model.CustomerEmailNormalized,
                    EmailHashSha256: model.CustomerEmailHashSha256,
                    PhoneNormalized: model.CustomerPhoneNormalized,
                    PhoneHashSha256: model.CustomerPhoneHashSha256),
                ct);

            custId = c;
        }

        var currency = new CurrencyCode(model.CurrencyCode);
        var total = new Money(model.TotalFinalPrice, currency);

        var lines = new List<OrderLine>
        {
            new OrderLine("ikas", "ikas order", 1, total)
        };

        var order = Order.Create(
            tenantId: tenantId,
            customerId: custId,
            channel: SalesChannel.Ikas,
            providerOrderId: model.ProviderOrderId,
            placedAtUtc: model.PlacedAtUtc,
            lines: lines,
            totalAmount: total,
            nowUtc: DateTimeOffset.UtcNow);

        await _db.Orders.AddAsync(order, ct);
        await _db.SaveChangesAsync(ct);

        return order.Id;
    }

    private void AddIdentityIfPresent(Customer customer, TenantId tenantId, IkasCustomerUpsert model, DateTimeOffset now)
    {
        if (!string.IsNullOrWhiteSpace(model.EmailNormalized) && !string.IsNullOrWhiteSpace(model.EmailHashSha256))
        {
            var hash = new IdentityHash(model.EmailHashSha256);
            var enc = _secrets.Protect(model.EmailNormalized);

            var identity = CustomerIdentity.Create(
                tenantId: tenantId,
                type: IdentityType.Email,
                valueHash: hash,
                valueEncrypted: enc,
                sourceProvider: ProviderType.Ikas,
                sourceExternalId: model.ProviderCustomerId,
                nowUtc: now);

            customer.AddOrTouchIdentity(identity, now);
        }

        if (!string.IsNullOrWhiteSpace(model.PhoneNormalized) && !string.IsNullOrWhiteSpace(model.PhoneHashSha256))
        {
            var hash = new IdentityHash(model.PhoneHashSha256);
            var enc = _secrets.Protect(model.PhoneNormalized);

            var identity = CustomerIdentity.Create(
                tenantId: tenantId,
                type: IdentityType.Phone,
                valueHash: hash,
                valueEncrypted: enc,
                sourceProvider: ProviderType.Ikas,
                sourceExternalId: model.ProviderCustomerId,
                nowUtc: now);

            customer.AddOrTouchIdentity(identity, now);
        }
    }
}
