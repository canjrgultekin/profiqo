// Path: backend/src/Profiqo.Infrastructure/Integrations/Ikas/IkasSyncStore.cs
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Linq;

using Microsoft.EntityFrameworkCore;

using Profiqo.Application.Abstractions.Crypto;
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
    private readonly ISecretProtector _secrets;

    public IkasSyncStore(ProfiqoDbContext db, IIdentityResolutionService resolver, ISecretProtector secrets)
    {
        _db = db;
        _resolver = resolver;
        _secrets = secrets;
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

        // Ikas guest/anon orders: customer can be null, but shipping/billing address usually contains name + phone.
        var extracted = ExtractPersonFromAddresses(model.ShippingAddressJson, model.BillingAddressJson);

        var firstName = NormalizeNameTokenOrNull(extracted.FirstName);
        var lastName = NormalizeNameTokenOrNull(extracted.LastName);

        // 1) Build identity inputs (email/phone)
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

        // Fallback: phone from address (if customer object is null)
        if (ids.All(x => x.Type != IdentityType.Phone))
        {
            var phoneNorm = NormalizePhone(extracted.Phone);
            if (!string.IsNullOrWhiteSpace(phoneNorm))
            {
                var phoneHash = Sha256Hex(phoneNorm);
                ids.Add(new IdentityInput(
                    IdentityType.Phone,
                    phoneNorm,
                    new IdentityHash(phoneHash),
                    ProviderType.Ikas,
                    model.ProviderOrderId));
            }
        }

        // 2) Find existing order first (prevents orphan customer creation on repeated sync)
        var existing = await _db.Orders
            .FirstOrDefaultAsync(o =>
                o.TenantId == tenantId &&
                o.Channel == SalesChannel.Ikas &&
                o.ProviderOrderId == model.ProviderOrderId, ct);

        if (existing is not null)
        {
            // Enrich current customer and (if identities point to another customer) re-attach the order.
            var targetCustomerId = await ResolveExistingOrderCustomerAsync(
                tenantId,
                existing.CustomerId,
                firstName,
                lastName,
                ids,
                now,
                ct);

            if (!targetCustomerId.Equals(existing.CustomerId))
                existing.ReassignCustomer(targetCustomerId, now);

            existing.SetProviderOrderStatus(model.OrderStatus, now);

            _db.Entry(existing).Property("ShippingAddressJson").CurrentValue =
                string.IsNullOrWhiteSpace(model.ShippingAddressJson) ? null : model.ShippingAddressJson;

            _db.Entry(existing).Property("BillingAddressJson").CurrentValue =
                string.IsNullOrWhiteSpace(model.BillingAddressJson) ? null : model.BillingAddressJson;

            await _db.SaveChangesAsync(ct);
            return existing.Id;
        }

        // 3) Resolve / create customer for a new order
        CustomerId customerId;

        if (ids.Count > 0)
        {
            customerId = await _resolver.ResolveOrCreateCustomerAsync(
                tenantId,
                firstName,
                lastName,
                ids,
                now,
                ct);
        }
        else
        {
            // No deterministic identity -> create anonymous customer, but keep name if available.
            var createdCustomer = Customer.Create(tenantId, now);
            if (firstName is not null || lastName is not null)
                createdCustomer.SetName(firstName, lastName, now);

            await _db.Customers.AddAsync(createdCustomer, ct);
            customerId = createdCustomer.Id;
        }

        var currency = new CurrencyCode(model.CurrencyCode);

        // 4) Build lines with new fields (productCategory, barcode, discount, line status)
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

    private async Task<CustomerId> ResolveExistingOrderCustomerAsync(
        TenantId tenantId,
        CustomerId currentCustomerId,
        string? firstName,
        string? lastName,
        IReadOnlyList<IdentityInput> identities,
        DateTimeOffset nowUtc,
        CancellationToken ct)
    {
        // If identities exist, see if they map to a different known customer.
        if (identities.Count > 0)
        {
            var matched = await FindCustomerByIdentityAsync(tenantId, identities, ct);
            if (matched is not null)
            {
                ApplyNameNonDestructive(matched, firstName, lastName, nowUtc);

                foreach (var i in identities)
                    await AddIdentitySafeAsync(matched, tenantId, i, nowUtc, ct);

                return matched.Id;
            }
        }

        var current = await _db.Customers
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == currentCustomerId, ct);

        if (current is null)
            return currentCustomerId;

        ApplyNameNonDestructive(current, firstName, lastName, nowUtc);

        foreach (var i in identities)
            await AddIdentitySafeAsync(current, tenantId, i, nowUtc, ct);

        return current.Id;
    }

    private async Task<Customer?> FindCustomerByIdentityAsync(TenantId tenantId, IReadOnlyList<IdentityInput> identities, CancellationToken ct)
    {
        var email = identities.FirstOrDefault(x => x.Type == IdentityType.Email);
        if (email is not null)
        {
            return await _db.Customers
                .AsTracking()
                .Where(c => c.TenantId == tenantId)
                .Where(c => c.Identities.Any(i => i.Type == IdentityType.Email && i.ValueHash == email.Hash))
                .FirstOrDefaultAsync(ct);
        }

        var phone = identities.FirstOrDefault(x => x.Type == IdentityType.Phone);
        if (phone is not null)
        {
            return await _db.Customers
                .AsTracking()
                .Where(c => c.TenantId == tenantId)
                .Where(c => c.Identities.Any(i => i.Type == IdentityType.Phone && i.ValueHash == phone.Hash))
                .FirstOrDefaultAsync(ct);
        }

        return null;
    }

    private static void ApplyNameNonDestructive(Customer customer, string? firstName, string? lastName, DateTimeOffset nowUtc)
    {
        var fn = NormalizeNameTokenOrNull(firstName);
        var ln = NormalizeNameTokenOrNull(lastName);

        if (fn is null && ln is null)
            return;

        var nextFirst = fn ?? customer.FirstName;
        var nextLast = ln ?? customer.LastName;

        if (nextFirst != customer.FirstName || nextLast != customer.LastName)
            customer.SetName(nextFirst, nextLast, nowUtc);
    }

    private async Task AddIdentitySafeAsync(Customer customer, TenantId tenantId, IdentityInput i, DateTimeOffset nowUtc, CancellationToken ct)
    {
        // Avoid unique index crashes if the same identity already exists on another customer.
        var existsOnOther = await _db.Customers
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.Id != customer.Id)
            .AnyAsync(c => c.Identities.Any(id => id.Type == i.Type && id.ValueHash == i.Hash), ct);

        if (existsOnOther)
            return;

        var enc = _secrets.Protect(i.NormalizedValue);

        var identity = CustomerIdentity.Create(
            tenantId: tenantId,
            type: i.Type,
            valueHash: i.Hash,
            valueEncrypted: enc,
            sourceProvider: i.SourceProvider,
            sourceExternalId: i.SourceExternalId,
            nowUtc: nowUtc);

        customer.AddOrTouchIdentity(identity, nowUtc);
    }

    private readonly record struct ExtractedPerson(string? FirstName, string? LastName, string? Phone);

    private static ExtractedPerson ExtractPersonFromAddresses(string? shippingJson, string? billingJson)
    {
        var s = ExtractPersonFromAddress(shippingJson);
        if (!string.IsNullOrWhiteSpace(s.FirstName) || !string.IsNullOrWhiteSpace(s.LastName) || !string.IsNullOrWhiteSpace(s.Phone))
            return s;

        return ExtractPersonFromAddress(billingJson);
    }

    private static ExtractedPerson ExtractPersonFromAddress(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new ExtractedPerson(null, null, null);

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var first = TryGetString(root, "firstName");
            var last = TryGetString(root, "lastName");
            var phone = TryGetString(root, "phone");

            var fullName = TryGetString(root, "fullName");
            if (!string.IsNullOrWhiteSpace(fullName) && (string.IsNullOrWhiteSpace(first) || string.IsNullOrWhiteSpace(last)))
            {
                var (f, l) = SplitFullName(fullName);
                first ??= f;
                last ??= l;
            }

            return new ExtractedPerson(first, last, phone);
        }
        catch
        {
            return new ExtractedPerson(null, null, null);
        }
    }

    private static (string? FirstName, string? LastName) SplitFullName(string fullName)
    {
        var t = (fullName ?? string.Empty).Trim();
        if (t.Length == 0) return (null, null);

        while (t.Contains("  ", StringComparison.Ordinal))
            t = t.Replace("  ", " ", StringComparison.Ordinal);

        var parts = t.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return (null, null);
        if (parts.Length == 1) return (parts[0], null);

        return (parts[0], string.Join(' ', parts[1..]));
    }

    private static string? TryGetString(JsonElement obj, string name)
        => obj.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private static string? NormalizeNameTokenOrNull(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var t = value.Trim();
        return t.Length == 0 ? null : t;
    }

    private static string NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return string.Empty;

        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("90")) return "+" + digits;
        if (digits.Length == 10) return "+90" + digits;
        return digits.Length > 0 ? "+" + digits : string.Empty;
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
