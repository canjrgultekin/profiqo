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

    public async Task UpsertOrderAsync(TenantId tenantId, TrendyolOrderUpsert order, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        // 1) Resolve/Create Customer via identities
        var emailNorm = NormalizeEmail(order.CustomerEmail);
        var phoneNorm = NormalizePhone(order.CustomerPhone);

        var emailHash = string.IsNullOrWhiteSpace(emailNorm) ? null : Sha256Hex(emailNorm);
        var phoneHash = string.IsNullOrWhiteSpace(phoneNorm) ? null : Sha256Hex(phoneNorm);

        var customer = await ResolveOrCreateCustomerAsync(tenantId, emailHash, phoneHash, now, ct);

        // 2) Idempotent order upsert by (tenant, channel, providerOrderId)
        var channel = SalesChannel.Trendyol;

        var existing = await _db.Orders
            .FirstOrDefaultAsync(x =>
                x.TenantId == tenantId &&
                x.Channel == channel &&
                x.ProviderOrderId == order.ProviderOrderId, ct);

        if (existing is not null)
            return;

        var currency = NormalizeCurrency(order.CurrencyCode);
        var totalAmount = new Money(order.TotalAmount, new CurrencyCode(currency));

        // 3) Build order lines using your Domain OrderLine constructor
        // OrderLine computes LineTotal = UnitPrice * Quantity.
        // To keep totals closer to Trendyol's line totals (discounted), we derive unit price from (LineTotal / qty) when possible.
        var lines = order.Lines.Select(l =>
        {
            var lc = NormalizeCurrency(l.CurrencyCode);

            var qty = l.Quantity <= 0 ? 1 : l.Quantity;

            decimal derivedUnit = 0m;
            if (l.LineTotal > 0m)
            {
                derivedUnit = l.LineTotal / qty;
            }
            else if (l.UnitPrice > 0m)
            {
                derivedUnit = l.UnitPrice;
            }

            // fallback: if still 0, use 0 (domain may allow, but ideally it shouldn't; keep it safe)
            var unitMoney = new Money(derivedUnit, new CurrencyCode(lc));

            var sku = (l.Sku ?? string.Empty).Trim();
            var name = string.IsNullOrWhiteSpace(l.ProductName) ? "unknown" : l.ProductName.Trim();

            return new OrderLine(sku, name, qty, unitMoney);
        }).ToList();

        // 4) Create order aggregate
        var created = Order.Create(
            tenantId: tenantId,
            customerId: customer.Id,
            channel: channel,
            providerOrderId: order.ProviderOrderId,
            placedAtUtc: order.PlacedAtUtc,
            lines: lines,
            totalAmount: totalAmount,
            nowUtc: now);

        await _db.Orders.AddAsync(created, ct);
        await _db.SaveChangesAsync(ct);
    }

    private async Task<Customer> ResolveOrCreateCustomerAsync(
        TenantId tenantId,
        string? emailHash,
        string? phoneHash,
        DateTimeOffset nowUtc,
        CancellationToken ct)
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
            {
                customer.AddOrTouchIdentity(
                    CustomerIdentity.Create(
                        tenantId: tenantId,
                        type: IdentityType.Email,
                        valueHash: new IdentityHash(emailHash),
                        valueEncrypted: null,
                        sourceProvider: ProviderType.Trendyol,
                        sourceExternalId: null,
                        nowUtc: nowUtc),
                    nowUtc);
            }

            if (!string.IsNullOrWhiteSpace(phoneHash))
            {
                customer.AddOrTouchIdentity(
                    CustomerIdentity.Create(
                        tenantId: tenantId,
                        type: IdentityType.Phone,
                        valueHash: new IdentityHash(phoneHash),
                        valueEncrypted: null,
                        sourceProvider: ProviderType.Trendyol,
                        sourceExternalId: null,
                        nowUtc: nowUtc),
                    nowUtc);
            }

            await _db.Customers.AddAsync(customer, ct);
            await _db.SaveChangesAsync(ct);
            return customer;
        }

        if (!string.IsNullOrWhiteSpace(emailHash))
        {
            customer.AddOrTouchIdentity(
                CustomerIdentity.Create(
                    tenantId: tenantId,
                    type: IdentityType.Email,
                    valueHash: new IdentityHash(emailHash),
                    valueEncrypted: null,
                    sourceProvider: ProviderType.Trendyol,
                    sourceExternalId: null,
                    nowUtc: nowUtc),
                nowUtc);
        }

        if (!string.IsNullOrWhiteSpace(phoneHash))
        {
            customer.AddOrTouchIdentity(
                CustomerIdentity.Create(
                    tenantId: tenantId,
                    type: IdentityType.Phone,
                    valueHash: new IdentityHash(phoneHash),
                    valueEncrypted: null,
                    sourceProvider: ProviderType.Trendyol,
                    sourceExternalId: null,
                    nowUtc: nowUtc),
                nowUtc);
        }

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
LIMIT 1;
";
        return await _db.Customers
            .FromSqlRaw(sql, tenantGuid, identityType, valueHash)
            .FirstOrDefaultAsync(ct);
    }

    private static string NormalizeEmail(string? email)
        => (email ?? string.Empty).Trim().ToLowerInvariant();

    private static string NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return string.Empty;

        var digits = new string(phone.Where(char.IsDigit).ToArray());

        if (digits.StartsWith("90")) return "+" + digits;
        if (digits.Length == 10) return "+90" + digits;

        return digits.Length > 0 ? "+" + digits : string.Empty;
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
