// Path: backend/src/Profiqo.Infrastructure/Integrations/Shopify/ShopifySyncStore.cs
using System.Security.Cryptography;
using System.Text;

using Microsoft.EntityFrameworkCore;

using Profiqo.Application.Abstractions.Integrations.Shopify;
using Profiqo.Application.Customers.IdentityResolution;
using Profiqo.Domain.Common.Ids;
using Profiqo.Domain.Common.Types;
using Profiqo.Domain.Customers;
using Profiqo.Domain.Integrations;
using Profiqo.Domain.Orders;
using Profiqo.Infrastructure.Persistence;
using Profiqo.Infrastructure.Persistence.Entities;

namespace Profiqo.Infrastructure.Integrations.Shopify;

public sealed class ShopifySyncStore : IShopifySyncStore
{
    private readonly ProfiqoDbContext _db;
    private readonly IIdentityResolutionService _resolver;

    public ShopifySyncStore(ProfiqoDbContext db, IIdentityResolutionService resolver)
    {
        _db = db;
        _resolver = resolver;
    }

    // ═══════════════════════════════════════════════════════════
    //  CUSTOMER UPSERT
    // ═══════════════════════════════════════════════════════════
    public async Task<CustomerId> UpsertCustomerAsync(TenantId tenantId, ShopifyCustomerUpsert model, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var ids = new List<IdentityInput>();

        if (!string.IsNullOrWhiteSpace(model.EmailNormalized) && !string.IsNullOrWhiteSpace(model.EmailHashSha256))
            ids.Add(new IdentityInput(IdentityType.Email, model.EmailNormalized, new IdentityHash(model.EmailHashSha256), ProviderType.Shopify, model.ProviderCustomerId));

        if (!string.IsNullOrWhiteSpace(model.PhoneNormalized) && !string.IsNullOrWhiteSpace(model.PhoneHashSha256))
            ids.Add(new IdentityInput(IdentityType.Phone, model.PhoneNormalized, new IdentityHash(model.PhoneHashSha256), ProviderType.Shopify, model.ProviderCustomerId));

        if (!string.IsNullOrWhiteSpace(model.ProviderCustomerId))
        {
            var pid = model.ProviderCustomerId.Trim();
            var hash = Sha256Hex($"shopify:{pid}");
            ids.Add(new IdentityInput(IdentityType.ProviderCustomerId, pid, new IdentityHash(hash), ProviderType.Shopify, pid));
        }

        var customerId = await _resolver.ResolveOrCreateCustomerAsync(tenantId, model.FirstName, model.LastName, ids, now, ct);

        if (!string.IsNullOrWhiteSpace(model.ProviderCustomerJson))
        {
            var customer = await _db.Customers.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == customerId, ct);
            if (customer is not null)
                _db.Entry(customer).Property("ProviderProfileJson").CurrentValue = model.ProviderCustomerJson;
        }

        await _db.SaveChangesAsync(ct);
        return customerId;
    }

    // ═══════════════════════════════════════════════════════════
    //  ORDER UPSERT
    // ═══════════════════════════════════════════════════════════
    public async Task<OrderId> UpsertOrderAsync(TenantId tenantId, ShopifyOrderUpsert model, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var ids = new List<IdentityInput>();

        if (!string.IsNullOrWhiteSpace(model.CustomerEmailNormalized) && !string.IsNullOrWhiteSpace(model.CustomerEmailHashSha256))
            ids.Add(new IdentityInput(IdentityType.Email, model.CustomerEmailNormalized, new IdentityHash(model.CustomerEmailHashSha256), ProviderType.Shopify, model.ProviderOrderId));

        if (!string.IsNullOrWhiteSpace(model.CustomerPhoneNormalized) && !string.IsNullOrWhiteSpace(model.CustomerPhoneHashSha256))
            ids.Add(new IdentityInput(IdentityType.Phone, model.CustomerPhoneNormalized, new IdentityHash(model.CustomerPhoneHashSha256), ProviderType.Shopify, model.ProviderOrderId));

        var statusStr = $"{model.FinancialStatus ?? "UNKNOWN"}/{model.FulfillmentStatus ?? "UNFULFILLED"}";

        // Existing order?
        var existing = await _db.Orders.FirstOrDefaultAsync(o =>
            o.TenantId == tenantId && o.Channel == SalesChannel.Shopify && o.ProviderOrderId == model.ProviderOrderId, ct);

        if (existing is not null)
        {
            existing.SetProviderOrderStatus(statusStr, now);
            _db.Entry(existing).Property("ShippingAddressJson").CurrentValue = model.ShippingAddressJson;
            _db.Entry(existing).Property("BillingAddressJson").CurrentValue = model.BillingAddressJson;
            _db.Entry(existing).Property("ProviderCustomerJson").CurrentValue = model.ProviderCustomerJson;
            await _db.SaveChangesAsync(ct);
            return existing.Id;
        }

        // Resolve customer
        CustomerId customerId;
        if (ids.Count > 0)
            customerId = await _resolver.ResolveOrCreateCustomerAsync(tenantId, null, null, ids, now, ct);
        else
        {
            var anon = Customer.Create(tenantId, now);
            await _db.Customers.AddAsync(anon, ct);
            customerId = anon.Id;
        }

        // Build lines
        var currency = NormCurrency(model.CurrencyCode);
        var lines = (model.Lines ?? Array.Empty<ShopifyOrderLineUpsert>())
            .Select(l =>
            {
                var lc = NormCurrency(l.CurrencyCode);
                return new OrderLine(
                    sku: string.IsNullOrWhiteSpace(l.Sku) ? "NA" : l.Sku.Trim(),
                    productName: string.IsNullOrWhiteSpace(l.ProductName) ? "unknown" : l.ProductName.Trim(),
                    quantity: l.Quantity <= 0 ? 1 : l.Quantity,
                    unitPrice: new Money(l.UnitPrice, new CurrencyCode(lc)),
                    productCategory: l.ProductCategory,
                    brandName: l.BrandName,
                    categoryNames: null,
                    barcode: string.IsNullOrWhiteSpace(l.Barcode) ? null : l.Barcode.Trim(),
                    discount: new Money(l.Discount, new CurrencyCode(lc)),
                    orderLineItemStatusName: string.IsNullOrWhiteSpace(l.OrderLineItemStatusName) ? null : l.OrderLineItemStatusName.Trim());
            }).ToList();

        if (lines.Count == 0)
            lines.Add(new OrderLine("NA", "unknown", 1, Money.Zero(new CurrencyCode(currency))));

        var order = Order.Create(tenantId, customerId, SalesChannel.Shopify, model.ProviderOrderId,
            model.PlacedAtUtc, lines, new Money(model.TotalFinalPrice, new CurrencyCode(currency)), now);

        order.SetProviderOrderStatus(statusStr, now);
        await _db.Orders.AddAsync(order, ct);

        _db.Entry(order).Property("ShippingAddressJson").CurrentValue = model.ShippingAddressJson;
        _db.Entry(order).Property("BillingAddressJson").CurrentValue = model.BillingAddressJson;
        _db.Entry(order).Property("ProviderCustomerJson").CurrentValue = model.ProviderCustomerJson;

        await _db.SaveChangesAsync(ct);
        return order.Id;
    }

    // ═══════════════════════════════════════════════════════════
    //  PRODUCT UPSERT (ikas pattern ile birebir aynı)
    // ═══════════════════════════════════════════════════════════
    public async Task UpsertProductAsync(TenantId tenantId, ProviderConnectionId connectionId, ShopifyProductUpsert model, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var tenantGuid = tenantId.Value;

        var productSet = _db.Set<ProductRow>();
        var variantSet = _db.Set<ProductVariantRow>();

        var existing = await productSet.FirstOrDefaultAsync(
            x => x.TenantId == tenantGuid && x.ProviderProductId == model.ProviderProductId, ct);

        Guid productId;

        if (existing is null)
        {
            productId = Guid.NewGuid();
            var catJson = string.IsNullOrWhiteSpace(model.ProductType) ? null : $"[{{\"name\":\"{EscapeJsonString(model.ProductType)}\"}}]";

            var row = new ProductRow(
                id: productId,
                tenantId: tenantGuid,
                providerProductId: model.ProviderProductId,
                name: model.Name,
                description: model.Description,
                brandId: null,
                brandName: model.Vendor,
                categoryIdsJson: null,
                categoriesJson: catJson,
                totalStock: model.TotalInventory,
                productVolumeDiscountId: null,
                providerCreatedAtMs: model.ProviderCreatedAt.ToUnixTimeMilliseconds(),
                providerUpdatedAtMs: model.ProviderUpdatedAt.ToUnixTimeMilliseconds(),
                nowUtc: now);

            await productSet.AddAsync(row, ct);
            await _db.SaveChangesAsync(ct);
        }
        else
        {
            productId = existing.Id;
            var catJson = string.IsNullOrWhiteSpace(model.ProductType) ? null : $"[{{\"name\":\"{EscapeJsonString(model.ProductType)}\"}}]";

            existing.Update(
                name: model.Name,
                description: model.Description,
                brandId: null,
                brandName: model.Vendor,
                categoryIdsJson: null,
                categoriesJson: catJson,
                totalStock: model.TotalInventory,
                productVolumeDiscountId: null,
                providerUpdatedAtMs: model.ProviderUpdatedAt.ToUnixTimeMilliseconds(),
                nowUtc: now);
        }

        // Variants
        foreach (var v in model.Variants)
        {
            var existingVariant = await variantSet.FirstOrDefaultAsync(
                x => x.TenantId == tenantGuid && x.ProviderVariantId == v.ProviderVariantId, ct);

            if (existingVariant is null)
            {
                var barcodeJson = string.IsNullOrWhiteSpace(v.Barcode) ? "[]" : $"[\"{EscapeJsonString(v.Barcode)}\"]";
                var priceVal = v.Price?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "0";
                var stockVal = v.InventoryQuantity?.ToString() ?? "0";

                var vRow = new ProductVariantRow(
                    id: Guid.NewGuid(),
                    tenantId: tenantGuid,
                    productId: productId,
                    providerVariantId: v.ProviderVariantId,
                    sku: v.Sku,
                    hsCode: null,
                    barcodeListJson: barcodeJson,
                    sellIfOutOfStock: null,
                    pricesJson: $"[{{\"sellPrice\":{priceVal}}}]",
                    stocksJson: $"[{{\"stockCount\":{stockVal}}}]",
                    providerCreatedAtMs: model.ProviderCreatedAt.ToUnixTimeMilliseconds(),
                    nowUtc: now);

                await variantSet.AddAsync(vRow, ct);
            }
            else
            {
                var barcodeJson = string.IsNullOrWhiteSpace(v.Barcode) ? "[]" : $"[\"{EscapeJsonString(v.Barcode)}\"]";
                var priceVal = v.Price?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "0";
                var stockVal = v.InventoryQuantity?.ToString() ?? "0";

                existingVariant.Update(
                    sku: v.Sku,
                    hsCode: null,
                    barcodeListJson: barcodeJson,
                    sellIfOutOfStock: null,
                    pricesJson: $"[{{\"sellPrice\":{priceVal}}}]",
                    stocksJson: $"[{{\"stockCount\":{stockVal}}}]",
                    nowUtc: now);
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    // ═══════════════════════════════════════════════════════════
    //  HELPERS
    // ═══════════════════════════════════════════════════════════
    private static string NormCurrency(string? c)
    {
        var x = (c ?? "TRY").Trim().ToUpperInvariant();
        return x.Length == 3 ? x : "TRY";
    }

    private static string EscapeJsonString(string s)
        => s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");

    private static string Sha256Hex(string v)
    {
        var b = SHA256.HashData(Encoding.UTF8.GetBytes(v));
        var sb = new StringBuilder(b.Length * 2);
        foreach (var x in b) sb.Append(x.ToString("x2"));
        return sb.ToString();
    }
}