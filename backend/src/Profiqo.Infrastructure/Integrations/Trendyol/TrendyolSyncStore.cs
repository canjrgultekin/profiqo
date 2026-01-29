// Path: backend/src/Profiqo.Infrastructure/Integrations/Trendyol/TrendyolSyncStore.cs
using System.Security.Cryptography;
using System.Text;

using Microsoft.EntityFrameworkCore;

using Profiqo.Application.Abstractions.Integrations.Trendyol;
using Profiqo.Domain.Common.Ids;
using Profiqo.Domain.Common.Types;
using Profiqo.Domain.Customers;
using Profiqo.Domain.Integrations;
using Profiqo.Domain.Orders;
using Profiqo.Infrastructure.Persistence;

namespace Profiqo.Infrastructure.Integrations.Trendyol;

internal sealed class TrendyolSyncStore : ITrendyolSyncStore
{
    private readonly ProfiqoDbContext _db;

    public TrendyolSyncStore(ProfiqoDbContext db)
    {
        _db = db;
    }

    public async Task UpsertOrderAsync(TenantId tenantId, TrendyolOrderUpsert model, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        // Resolve/Create Customer
        var emailNorm = NormalizeEmail(model.CustomerEmail);
        var phoneNorm = NormalizePhone(model.CustomerPhone);

        var emailHash = string.IsNullOrWhiteSpace(emailNorm) ? null : Sha256Hex(emailNorm);
        var phoneHash = string.IsNullOrWhiteSpace(phoneNorm) ? null : Sha256Hex(phoneNorm);

        var customer = await ResolveOrCreateCustomerAsync(tenantId, emailHash, phoneHash, now, ct);

        if (!string.IsNullOrWhiteSpace(model.CustomerFirstName) || !string.IsNullOrWhiteSpace(model.CustomerLastName))
            customer.SetName(model.CustomerFirstName, model.CustomerLastName, now);

        // ProviderOrderId = shipmentPackageId string (unique)
        var providerOrderId = (model.ShipmentPackageId ?? "").Trim();
        if (string.IsNullOrWhiteSpace(providerOrderId))
            providerOrderId = Guid.NewGuid().ToString("N");

        var channel = SalesChannel.Trendyol;

        var exists = await _db.Orders.AsNoTracking().AnyAsync(x =>
            x.TenantId == tenantId &&
            x.Channel == channel &&
            x.ProviderOrderId == providerOrderId, ct);

        if (exists)
            return;

        var currency = NormalizeCurrency(model.CurrencyCode);
        var totalAmount = new Money(model.TotalPrice, new CurrencyCode(currency));

        var lines = (model.Lines ?? Array.Empty<TrendyolOrderLineUpsert>())
            .Where(l => l is not null)
            .Select(l =>
            {
                var lc = NormalizeCurrency(l.CurrencyCode);
                var qty = l.Quantity <= 0 ? 1 : l.Quantity;

                var unitMoney = new Money(l.UnitPrice, new CurrencyCode(lc));

                var sku = string.IsNullOrWhiteSpace(l.Sku) ? "NA" : l.Sku.Trim();
                var name = string.IsNullOrWhiteSpace(l.ProductName) ? "unknown" : l.ProductName.Trim();

                return new OrderLine(sku, name, qty, unitMoney);
            })
            .ToList();

        // If Trendyol returns empty lines for some packages, still persist order with no lines is not desired.
        // We'll enforce at least 1 line to keep schema consistent.
        if (lines.Count == 0)
        {
            // create a minimal synthetic line (doesn't break domain invariants)
            lines.Add(new OrderLine("NA", "unknown", 1, Money.Zero(new CurrencyCode(currency))));
        }

        var created = Order.Create(
            tenantId: tenantId,
            customerId: customer.Id,
            channel: channel,
            providerOrderId: providerOrderId,
            placedAtUtc: model.OrderDateUtc,
            lines: lines,
            totalAmount: totalAmount,
            nowUtc: now);

        await _db.Orders.AddAsync(created, ct);
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
LIMIT 1
";
        return await _db.Customers.FromSqlRaw(sql, tenantGuid, identityType, valueHash).FirstOrDefaultAsync(ct);
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
