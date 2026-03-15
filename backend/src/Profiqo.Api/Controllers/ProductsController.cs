// Path: backend/src/Profiqo.Api/Controllers/ProductsController.cs
using System.Text.Json;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using Profiqo.Api.Security;
using Profiqo.Application.Abstractions.Tenancy;
using Profiqo.Infrastructure.Persistence;
using Profiqo.Infrastructure.Persistence.Entities;

namespace Profiqo.Api.Controllers;

[ApiController]
[Route("api/products")]
[Authorize(Policy = AuthorizationPolicies.ReportAccess)]
public sealed class ProductsController : ControllerBase
{
    private readonly ProfiqoDbContext _db;
    private readonly ITenantContext _tenant;

    public ProductsController(ProfiqoDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? q = null,
        [FromQuery] string? brand = null,
        [FromQuery] string? category = null,
        CancellationToken ct = default)
    {
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null) return BadRequest(new { message = "Tenant context missing." });

        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 200 ? 25 : pageSize;
        var tenantGuid = tenantId.Value.Value;

        var query = _db.Set<ProductRow>().AsNoTracking()
            .Where(x => x.TenantId == tenantGuid);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var s = q.Trim();
            query = query.Where(x =>
                EF.Functions.ILike(x.Name, $"%{s}%") ||
                (x.BrandName != null && EF.Functions.ILike(x.BrandName, $"%{s}%")));
        }

        if (!string.IsNullOrWhiteSpace(brand))
        {
            var b = brand.Trim();
            query = query.Where(x => x.BrandName != null && EF.Functions.ILike(x.BrandName, $"%{b}%"));
        }

        // Category filter: JSON contains check on categories_json
        // We do this as a raw EF.Functions approach since PostgreSQL jsonb supports this
        if (!string.IsNullOrWhiteSpace(category))
        {
            var c = category.Trim();
            query = query.Where(x =>
                x.CategoriesJson != null &&
                EF.Functions.ILike(x.CategoriesJson, $"%{c}%"));
        }

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(x => x.ProviderUpdatedAtMs)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                productId = x.Id,
                providerProductId = x.ProviderProductId,
                name = x.Name,
                description = x.Description,
                brandId = x.BrandId,
                brandName = x.BrandName,
                totalStock = x.TotalStock,
                productVolumeDiscountId = x.ProductVolumeDiscountId,
                providerCreatedAtMs = x.ProviderCreatedAtMs,
                providerUpdatedAtMs = x.ProviderUpdatedAtMs,
                createdAtUtc = x.CreatedAtUtc,
                updatedAtUtc = x.UpdatedAtUtc
            })
            .ToListAsync(ct);

        // Load categories separately to parse JSON
        var productIds = items.Select(x => x.productId).ToList();
        var categoryMap = await _db.Set<ProductRow>().AsNoTracking()
            .Where(x => x.TenantId == tenantGuid && productIds.Contains(x.Id))
            .Select(x => new { x.Id, x.CategoriesJson })
            .ToDictionaryAsync(x => x.Id, x => x.CategoriesJson, ct);

        // Load variant counts
        var variantCounts = await _db.Set<ProductVariantRow>().AsNoTracking()
            .Where(x => x.TenantId == tenantGuid && productIds.Contains(x.ProductId))
            .GroupBy(x => x.ProductId)
            .Select(g => new { productId = g.Key, count = g.Count() })
            .ToDictionaryAsync(x => x.productId, x => x.count, ct);

        var mapped = items.Select(x =>
        {
            List<object>? categories = null;
            if (categoryMap.TryGetValue(x.productId, out var cJson) && !string.IsNullOrWhiteSpace(cJson))
            {
                try
                {
                    categories = JsonSerializer.Deserialize<List<object>>(cJson);
                }
                catch { /* ignore malformed json */ }
            }

            return new
            {
                x.productId,
                x.providerProductId,
                x.name,
                x.description,
                x.brandId,
                x.brandName,
                categories,
                x.totalStock,
                variantCount = variantCounts.TryGetValue(x.productId, out var vc) ? vc : 0,
                x.productVolumeDiscountId,
                x.providerCreatedAtMs,
                x.providerUpdatedAtMs,
                x.createdAtUtc,
                x.updatedAtUtc
            };
        });

        return Ok(new { page, pageSize, total, items = mapped });
    }

    [HttpGet("{productId:guid}")]
    public async Task<IActionResult> Get([FromRoute] Guid productId, CancellationToken ct)
    {
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null) return BadRequest(new { message = "Tenant context missing." });

        var tenantGuid = tenantId.Value.Value;

        var product = await _db.Set<ProductRow>().AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantGuid && x.Id == productId, ct);

        if (product is null) return NotFound(new { message = "Product not found." });

        var variants = await _db.Set<ProductVariantRow>().AsNoTracking()
            .Where(x => x.TenantId == tenantGuid && x.ProductId == productId)
            .OrderBy(x => x.CreatedAtUtc)
            .Select(v => new
            {
                variantId = v.Id,
                providerVariantId = v.ProviderVariantId,
                sku = v.Sku,
                hsCode = v.HsCode,
                barcodeListJson = v.BarcodeListJson,
                sellIfOutOfStock = v.SellIfOutOfStock,
                pricesJson = v.PricesJson,
                stocksJson = v.StocksJson,
                providerCreatedAtMs = v.ProviderCreatedAtMs,
                createdAtUtc = v.CreatedAtUtc,
                updatedAtUtc = v.UpdatedAtUtc
            })
            .ToListAsync(ct);

        object? ParseJson(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try { return JsonSerializer.Deserialize<object>(json); }
            catch { return null; }
        }

        var mappedVariants = variants.Select(v => new
        {
            v.variantId,
            v.providerVariantId,
            v.sku,
            v.hsCode,
            barcodeList = ParseJson(v.barcodeListJson),
            v.sellIfOutOfStock,
            prices = ParseJson(v.pricesJson),
            stocks = ParseJson(v.stocksJson),
            v.providerCreatedAtMs,
            v.createdAtUtc,
            v.updatedAtUtc
        });

        return Ok(new
        {
            productId = product.Id,
            providerProductId = product.ProviderProductId,
            name = product.Name,
            description = product.Description,
            brandId = product.BrandId,
            brandName = product.BrandName,
            categories = ParseJson(product.CategoriesJson),
            categoryIds = ParseJson(product.CategoryIdsJson),
            totalStock = product.TotalStock,
            productVolumeDiscountId = product.ProductVolumeDiscountId,
            providerCreatedAtMs = product.ProviderCreatedAtMs,
            providerUpdatedAtMs = product.ProviderUpdatedAtMs,
            createdAtUtc = product.CreatedAtUtc,
            updatedAtUtc = product.UpdatedAtUtc,
            variants = mappedVariants
        });
    }
}