// Path: backend/src/Profiqo.Infrastructure/StorefrontEvents/StorefrontCheckoutProjector.cs
using System.Security.Cryptography;
using System.Text;

using Microsoft.EntityFrameworkCore;

using Profiqo.Application.StorefrontEvents;
using Profiqo.Domain.Common.Ids;
using Profiqo.Domain.Common.Types;
using Profiqo.Domain.Customers;
using Profiqo.Domain.Integrations;
using Profiqo.Domain.Orders;
using Profiqo.Infrastructure.Persistence;

namespace Profiqo.Infrastructure.StorefrontEvents;

public sealed class StorefrontCheckoutProjector : IStorefrontCheckoutProjector
{
    private readonly ProfiqoDbContext _db;

    public StorefrontCheckoutProjector(ProfiqoDbContext db)
    {
        _db = db;
    }

    public async Task ProjectCompleteCheckoutAsync(
        TenantId tenantId,
        Guid? resolvedCustomerId,
        string eventDataJson,
        DateTimeOffset occurredAtUtc,
        CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        using var doc = System.Text.Json.JsonDocument.Parse(eventDataJson);
        var root = doc.RootElement;

        var orderId = ReadString(root, "orderId");
        if (string.IsNullOrWhiteSpace(orderId))
            return;
        var orderNumber = ReadString(root, "orderNumber");

        var currency = NormalizeCurrency(ReadString(root, "currency") ?? "TRY");
        var totalPrice = ReadDecimal(root, "totalPrice") ?? 0m;

        // Customer context (_customer)
        var customerId = resolvedCustomerId;

        string? email = null;
        string? phone = null;
        string? firstName = null;
        string? lastName = null;
        string? providerCustomerId = null;

        if (root.TryGetProperty("_customer", out var c) && c.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            providerCustomerId = ReadString(c, "id");
            email = ReadString(c, "email");
            phone = ReadString(c, "phone");
            firstName = ReadString(c, "firstName");
            lastName = ReadString(c, "lastName");
        }

        if (customerId is null)
        {
            customerId = await ResolveOrCreateAnonymousCustomerAsync(
                tenantId, email, phone, firstName, lastName, providerCustomerId, now, ct);
        }

        // Idempotency: providerOrderId = ikas orderId
        var providerOrderId = orderNumber;

        // IMPORTANT: Order entity materialize ETME.
        // Poison order row yüzünden patlamasın diye scalar exists check yap.
        var channel = SalesChannel.Ikas;

        if (await OrderExistsAsync(tenantId, channel, providerOrderId, ct))
            return;

        // Build lines from event_data.items
        var lines = new List<OrderLine>();

        if (root.TryGetProperty("items", out var items) && items.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            foreach (var it in items.EnumerateArray())
            {
                var price = ReadDecimal(it, "price") ?? 0m;
                var qty = (int)(ReadInt64(it, "quantity") ?? 1);

                var productId = ReadString(it, "productId");
                var variantId = ReadString(it, "variantId");
                var productName = ReadString(it, "productName") ?? "unknown";

                var sku =
                    !string.IsNullOrWhiteSpace(variantId) ? variantId.Trim()
                    : !string.IsNullOrWhiteSpace(productId) ? productId.Trim()
                    : "NA";

                var unitMoney = new Money(price, new CurrencyCode(currency));
                var discountMoney = Money.Zero(new CurrencyCode(currency));

                lines.Add(new OrderLine(
                    sku: sku,
                    productName: productName.Trim(),
                    quantity: qty <= 0 ? 1 : qty,
                    unitPrice: unitMoney,
                    productCategory: null,
                    brandName: null,
                    categoryNames: null,
                    barcode: null,
                    discount: discountMoney,
                    orderLineItemStatusName: "CREATED"
                ));
            }
        }

        if (lines.Count == 0)
            lines.Add(new OrderLine("NA", "unknown", 1, Money.Zero(new CurrencyCode(currency))));

        var totalAmount = new Money(totalPrice, new CurrencyCode(currency));

        var created = Order.Create(
            tenantId: tenantId,
            customerId: new CustomerId(customerId!.Value),
            channel: channel,
            providerOrderId: providerOrderId,
            placedAtUtc: occurredAtUtc,
            lines: lines,
            totalAmount: totalAmount,
            nowUtc: now);

        created.SetProviderOrderStatus("CREATED", now);

        await _db.Orders.AddAsync(created, ct);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Büyük olasılıkla unique constraint (tenant+channel+providerOrderId).
            // Aynı event iki kere geldiyse ignore.
        }
    }

    // ✅ Scalar exists check: Order entity load yok.
    private async Task<bool> OrderExistsAsync(TenantId tenantId, SalesChannel channel, string providerOrderId, CancellationToken ct)
    {
        // EF scalar query subquery’yi SELECT s."Value" ile sarar, alias şart.
        const string sql = @"
SELECT 1 AS ""Value""
FROM public.orders o
WHERE o.tenant_id = {0}
  AND o.channel = {1}
  AND o.provider_order_id = {2}
LIMIT 1";

        // channel db’de smallint/int olabilir, enum’u short’a düşürmek genelde güvenli.
        var channelVal = (short)channel;

        var hit = await _db.Database
            .SqlQueryRaw<int?>(sql, tenantId.Value, channelVal, providerOrderId)
            .FirstOrDefaultAsync(ct);

        return hit.HasValue;
    }

    private async Task<Guid> ResolveOrCreateAnonymousCustomerAsync(
        TenantId tenantId,
        string? email,
        string? phone,
        string? firstName,
        string? lastName,
        string? providerCustomerId,
        DateTimeOffset nowUtc,
        CancellationToken ct)
    {
        var emailNorm = NormalizeEmail(email);
        var phoneNorm = NormalizePhone(phone);

        var emailHash = string.IsNullOrWhiteSpace(emailNorm) ? null : Sha256Hex(emailNorm);
        var phoneHash = string.IsNullOrWhiteSpace(phoneNorm) ? null : Sha256Hex(phoneNorm);

        Guid? existingCustomerGuid = null;

        if (!string.IsNullOrWhiteSpace(emailHash))
            existingCustomerGuid = await FindCustomerIdByIdentityHashAsync(tenantId.Value, (short)IdentityType.Email, emailHash, ct);

        if (existingCustomerGuid is null && !string.IsNullOrWhiteSpace(phoneHash))
            existingCustomerGuid = await FindCustomerIdByIdentityHashAsync(tenantId.Value, (short)IdentityType.Phone, phoneHash, ct);

        // existing varsa entity load yok, direkt id dön
        if (existingCustomerGuid is not null)
            return existingCustomerGuid.Value;

        var customer = Customer.Create(tenantId, nowUtc);

        if (!string.IsNullOrWhiteSpace(firstName) || !string.IsNullOrWhiteSpace(lastName))
            customer.SetName(firstName, lastName, nowUtc);

        if (!string.IsNullOrWhiteSpace(emailHash))
            customer.AddOrTouchIdentity(
                CustomerIdentity.Create(tenantId, IdentityType.Email, new IdentityHash(emailHash), null, ProviderType.Pixel, providerCustomerId, nowUtc),
                nowUtc);

        if (!string.IsNullOrWhiteSpace(phoneHash))
            customer.AddOrTouchIdentity(
                CustomerIdentity.Create(tenantId, IdentityType.Phone, new IdentityHash(phoneHash), null, ProviderType.Pixel, providerCustomerId, nowUtc),
                nowUtc);

        await _db.Customers.AddAsync(customer, ct);
        await _db.SaveChangesAsync(ct);
        return customer.Id.Value;
    }

    private async Task<Guid?> FindCustomerIdByIdentityHashAsync(Guid tenantGuid, short identityType, string valueHash, CancellationToken ct)
    {
        const string sql = @"
SELECT i.customer_id AS ""Value""
FROM public.customer_identities i
WHERE i.tenant_id = {0} AND i.type = {1} AND i.value_hash = {2}
LIMIT 1";

        return await _db.Database
            .SqlQueryRaw<Guid?>(sql, tenantGuid, identityType, valueHash)
            .FirstOrDefaultAsync(ct);
    }

    private static string? ReadString(System.Text.Json.JsonElement obj, string name)
        => obj.ValueKind == System.Text.Json.JsonValueKind.Object &&
           obj.TryGetProperty(name, out var p) &&
           p.ValueKind == System.Text.Json.JsonValueKind.String
            ? p.GetString()
            : null;

    private static long? ReadInt64(System.Text.Json.JsonElement obj, string name)
        => obj.ValueKind == System.Text.Json.JsonValueKind.Object &&
           obj.TryGetProperty(name, out var p) &&
           p.ValueKind == System.Text.Json.JsonValueKind.Number
            ? p.GetInt64()
            : null;

    private static decimal? ReadDecimal(System.Text.Json.JsonElement obj, string name)
    {
        if (obj.ValueKind != System.Text.Json.JsonValueKind.Object) return null;
        if (!obj.TryGetProperty(name, out var p)) return null;

        if (p.ValueKind == System.Text.Json.JsonValueKind.Number && p.TryGetDecimal(out var d)) return d;
        if (p.ValueKind == System.Text.Json.JsonValueKind.String &&
            decimal.TryParse(p.GetString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var x))
            return x;

        return null;
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