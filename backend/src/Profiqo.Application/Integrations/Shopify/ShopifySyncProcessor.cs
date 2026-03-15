// Path: backend/src/Profiqo.Application/Integrations/Shopify/ShopifySyncProcessor.cs
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Options;

using Profiqo.Application.Abstractions.Crypto;
using Profiqo.Application.Abstractions.Integrations.Shopify;
using Profiqo.Application.Abstractions.Persistence;
using Profiqo.Application.Abstractions.Persistence.Repositories;
using Profiqo.Domain.Common.Ids;
using Profiqo.Domain.Integrations;

namespace Profiqo.Application.Integrations.Shopify;

public sealed class ShopifySyncProcessor : IShopifySyncProcessor
{
    private readonly IProviderConnectionRepository _connections;
    private readonly ISecretProtector _secrets;
    private readonly IShopifyGraphqlClient _client;
    private readonly IShopifyTokenService _tokenService;
    private readonly IShopifySyncStore _store;
    private readonly IIntegrationJobRepository _jobs;
    private readonly IIntegrationCursorRepository _cursors;
    private readonly ShopifyOptions _opts;

    private const string CustomerCursorKey = "shopify.customers.cursor.updatedAt";
    private const string OrderCursorKey = "shopify.orders.cursor.updatedAt";
    private const string ProductCursorKey = "shopify.products.cursor.updatedAt";

    private sealed record ShopifyCreds(string ClientId, string ClientSecret);

    public ShopifySyncProcessor(
        IProviderConnectionRepository connections, ISecretProtector secrets,
        IShopifyGraphqlClient client, IShopifyTokenService tokenService,
        IShopifySyncStore store, IIntegrationJobRepository jobs,
        IIntegrationCursorRepository cursors, IOptions<ShopifyOptions> opts)
    {
        _connections = connections; _secrets = secrets; _client = client;
        _tokenService = tokenService; _store = store; _jobs = jobs;
        _cursors = cursors; _opts = opts.Value;
    }

    // ═══════════════════════════════════════════════════════════
    //  CONNECTION RESOLVE + AUTO TOKEN REFRESH
    // ═══════════════════════════════════════════════════════════
    // DB Layout:
    //   AccessToken         → encrypted { ClientId, ClientSecret }  (kalıcı)
    //   RefreshToken        → encrypted shpat_xxx                   (24h rotate)
    //   AccessTokenExpiresAtUtc → shpat expire zamanı
    // ═══════════════════════════════════════════════════════════
    private async Task<(string ShopName, string AccessToken, ProviderConnectionId ConnId)> ResolveConnectionAsync(
        TenantId tenantId, Guid connectionId, CancellationToken ct)
    {
        var connId = new ProviderConnectionId(connectionId);
        var conn = await _connections.GetByIdAsync(connId, ct);
        if (conn is null || conn.TenantId != tenantId || conn.ProviderType != ProviderType.Shopify)
            throw new InvalidOperationException("Shopify connection not found for tenant.");

        var shopName = conn.ExternalAccountId ?? throw new InvalidOperationException("ShopName missing.");

        // Cached token hala geçerli mi?
        var bufferMin = _opts.TokenRefreshBufferMinutes > 0 ? _opts.TokenRefreshBufferMinutes : 30;
        var tokenValid = !string.IsNullOrWhiteSpace(conn.RefreshToken.ToString())
            && conn.AccessTokenExpiresAtUtc.HasValue
            && conn.AccessTokenExpiresAtUtc.Value > DateTimeOffset.UtcNow.AddMinutes(bufferMin);

        if (tokenValid)
        {
            var cachedToken = _secrets.Unprotect(conn.RefreshToken!);
            return (shopName, cachedToken, connId);
        }

        // Token expired/yok: creds'ten yeni token al
        var credsJson = _secrets.Unprotect(conn.AccessToken);
        var creds = JsonSerializer.Deserialize<ShopifyCreds>(credsJson)
            ?? throw new InvalidOperationException("Shopify credentials invalid.");

        var result = await _tokenService.AcquireTokenAsync(shopName, creds.ClientId, creds.ClientSecret, ct);
        var encToken = _secrets.Protect(result.AccessToken);

        // AccessToken (creds) aynı kalır, RefreshToken (shpat) güncellenir
        conn.RotateTokens(conn.AccessToken, encToken, result.ExpiresAtUtc, DateTimeOffset.UtcNow);

        return (shopName, result.AccessToken, connId);
    }

    // ═══════════════════════════════════════════════════════════
    //  CUSTOMERS
    // ═══════════════════════════════════════════════════════════
    public async Task<int> SyncCustomersAsync(Guid jobId, TenantId tenantId, Guid connectionId, int pageSize, int maxPages, CancellationToken ct)
    {
        var (shopName, accessToken, connId) = await ResolveConnectionAsync(tenantId, connectionId, ct);
        var cursorDate = await GetCursorOrNull(tenantId, connId, CustomerCursorKey, ct);
        var sinceDate = cursorDate ?? DateTimeOffset.UtcNow.AddDays(-_opts.BackfillDays).ToString("o");

        const string query = @"
query($first: Int!, $after: String, $qf: String) {
  customers(first: $first, after: $after, query: $qf, sortKey: UPDATED_AT) {
    pageInfo { hasNextPage endCursor }
    edges { node { id firstName lastName email phone updatedAt createdAt
      defaultAddress { address1 address2 city province provinceCode country countryCodeV2 zip phone }
    }}
  }
}";
        var safeSize = Math.Min(pageSize <= 0 ? _opts.DefaultPageSize : pageSize, 250);
        var processed = 0;
        string? afterCursor = null;
        var maxSeen = cursorDate is not null ? DateTimeOffset.Parse(cursorDate) : DateTimeOffset.MinValue;

        for (var page = 0; page < maxPages; page++)
        {
            ct.ThrowIfCancellationRequested();
            var vars = new { first = safeSize, after = afterCursor, qf = $"updated_at:>\"{sinceDate}\"" };
            using var doc = await _client.QueryAsync(shopName, accessToken, query, vars, ct);
            ThrowOnErrors(doc);
            var customers = doc.RootElement.GetProperty("data").GetProperty("customers");
            var edges = customers.GetProperty("edges");
            if (edges.ValueKind != JsonValueKind.Array || edges.GetArrayLength() == 0) break;
            foreach (var edge in edges.EnumerateArray())
            {
                ct.ThrowIfCancellationRequested();
                var n = edge.GetProperty("node");
                if (DateTimeOffset.TryParse(Str(n, "updatedAt"), out var ua) && ua > maxSeen) maxSeen = ua;
                var emailN = NormEmail(Str(n, "email")); var phoneN = NormPhone(Str(n, "phone"));
                await _store.UpsertCustomerAsync(tenantId, new ShopifyCustomerUpsert(
                    ExtractGid(Str(n, "id")), Str(n, "firstName"), Str(n, "lastName"),
                    emailN, Hash(emailN), phoneN, Hash(phoneN), n.GetRawText()), ct);
                processed++;
            }
            await _jobs.MarkProgressAsync(jobId, processed, ct);
            var pi = customers.GetProperty("pageInfo");
            if (!Bool(pi, "hasNextPage")) break;
            afterCursor = Str(pi, "endCursor");
        }
        await SaveCursor(tenantId, connId, CustomerCursorKey, maxSeen, ct);
        return processed;
    }

    // ═══════════════════════════════════════════════════════════
    //  ORDERS
    // ═══════════════════════════════════════════════════════════
    public async Task<int> SyncOrdersAsync(Guid jobId, TenantId tenantId, Guid connectionId, int pageSize, int maxPages, CancellationToken ct)
    {
        var (shopName, accessToken, connId) = await ResolveConnectionAsync(tenantId, connectionId, ct);
        var cursorDate = await GetCursorOrNull(tenantId, connId, OrderCursorKey, ct);
        var sinceDate = cursorDate ?? DateTimeOffset.UtcNow.AddDays(-_opts.BackfillDays).ToString("o");

        const string query = @"
query($first: Int!, $after: String, $qf: String) {
  orders(first: $first, after: $after, query: $qf, sortKey: UPDATED_AT) {
    pageInfo { hasNextPage endCursor }
    edges { node {
      id name createdAt updatedAt
      displayFinancialStatus displayFulfillmentStatus
      totalPriceSet { shopMoney { amount currencyCode } }
      customer { id firstName lastName email phone }
      shippingAddress { address1 address2 city province provinceCode country countryCodeV2 zip phone name }
      billingAddress { address1 address2 city province provinceCode country countryCodeV2 zip phone name }
      lineItems(first: 100) { edges { node {
        title quantity sku
        variant { id sku barcode product { id } }
        originalUnitPriceSet { shopMoney { amount currencyCode } }
        discountedUnitPriceSet { shopMoney { amount currencyCode } }
        totalDiscountSet { shopMoney { amount currencyCode } }
      }}}
    }}
  }
}";
        var safeSize = Math.Min(pageSize <= 0 ? _opts.DefaultPageSize : pageSize, 250);
        var processed = 0; string? afterCursor = null;
        var maxSeen = cursorDate is not null ? DateTimeOffset.Parse(cursorDate) : DateTimeOffset.MinValue;

        for (var page = 0; page < maxPages; page++)
        {
            ct.ThrowIfCancellationRequested();
            using var doc = await _client.QueryAsync(shopName, accessToken, query, new { first = safeSize, after = afterCursor, qf = $"updated_at:>\"{sinceDate}\"" }, ct);
            ThrowOnErrors(doc);
            var orders = doc.RootElement.GetProperty("data").GetProperty("orders");
            var edges = orders.GetProperty("edges");
            if (edges.ValueKind != JsonValueKind.Array || edges.GetArrayLength() == 0) break;

            foreach (var edge in edges.EnumerateArray())
            {
                ct.ThrowIfCancellationRequested();
                var n = edge.GetProperty("node");
                var provOrderId = Str(n, "name") ?? ExtractGid(Str(n, "id"));
                var placedAt = DateTimeOffset.TryParse(Str(n, "createdAt"), out var ca) ? ca : DateTimeOffset.UtcNow;
                if (DateTimeOffset.TryParse(Str(n, "updatedAt"), out var ua) && ua > maxSeen) maxSeen = ua;
                var finSt = Str(n, "displayFinancialStatus"); var fulSt = Str(n, "displayFulfillmentStatus");
                var currency = "TRY"; decimal totalPrice = 0m;
                if (n.TryGetProperty("totalPriceSet", out var tps) && tps.TryGetProperty("shopMoney", out var sm))
                { totalPrice = DecStr(sm, "amount") ?? 0m; currency = Str(sm, "currencyCode") ?? "TRY"; }
                string? custJson = null, emailN = null, emailH = null, phoneN = null, phoneH = null;
                if (n.TryGetProperty("customer", out var cust) && cust.ValueKind == JsonValueKind.Object)
                { custJson = cust.GetRawText(); emailN = NormEmail(Str(cust, "email")); phoneN = NormPhone(Str(cust, "phone")); emailH = Hash(emailN); phoneH = Hash(phoneN); }
                string? shipJ = n.TryGetProperty("shippingAddress", out var sa) && sa.ValueKind == JsonValueKind.Object ? sa.GetRawText() : null;
                string? billJ = n.TryGetProperty("billingAddress", out var ba) && ba.ValueKind == JsonValueKind.Object ? ba.GetRawText() : null;
                var lines = new List<ShopifyOrderLineUpsert>();
                if (n.TryGetProperty("lineItems", out var li) && li.TryGetProperty("edges", out var liE) && liE.ValueKind == JsonValueKind.Array)
                    foreach (var le in liE.EnumerateArray())
                    {
                        var ln = le.GetProperty("node"); var title = Str(ln, "title") ?? "unknown"; var qty = (int)(Int64(ln, "quantity") ?? 1); var sku = Str(ln, "sku");
                        string? varId = null, prodId = null, barcode = null;
                        if (ln.TryGetProperty("variant", out var v) && v.ValueKind == JsonValueKind.Object) { varId = ExtractGid(Str(v, "id")); sku ??= Str(v, "sku"); barcode = Str(v, "barcode"); if (v.TryGetProperty("product", out var p) && p.ValueKind == JsonValueKind.Object) prodId = ExtractGid(Str(p, "id")); }
                        decimal unitP = 0m; var lCur = currency;
                        if (ln.TryGetProperty("originalUnitPriceSet", out var ou) && ou.TryGetProperty("shopMoney", out var ous)) { unitP = DecStr(ous, "amount") ?? 0m; lCur = Str(ous, "currencyCode") ?? currency; }
                        decimal discU = unitP; if (ln.TryGetProperty("discountedUnitPriceSet", out var du) && du.TryGetProperty("shopMoney", out var dus)) discU = DecStr(dus, "amount") ?? unitP;
                        decimal disc = 0m; if (ln.TryGetProperty("totalDiscountSet", out var td) && td.TryGetProperty("shopMoney", out var tds)) disc = DecStr(tds, "amount") ?? 0m;
                        lines.Add(new ShopifyOrderLineUpsert(sku ?? "NA", title, qty, unitP, discU * qty, lCur, varId, prodId, null, null, barcode, disc, fulSt));
                    }
                await _store.UpsertOrderAsync(tenantId, new ShopifyOrderUpsert(provOrderId, placedAt, currency, totalPrice, finSt, fulSt, custJson, emailN, emailH, phoneN, phoneH, lines, shipJ, billJ), ct);
                processed++; if (processed % 25 == 0) await _jobs.MarkProgressAsync(jobId, processed, ct);
            }
            await _jobs.MarkProgressAsync(jobId, processed, ct);
            var pi = orders.GetProperty("pageInfo"); if (!Bool(pi, "hasNextPage")) break; afterCursor = Str(pi, "endCursor");
        }
        await SaveCursor(tenantId, connId, OrderCursorKey, maxSeen, ct);
        return processed;
    }

    // ═══════════════════════════════════════════════════════════
    //  PRODUCTS
    // ═══════════════════════════════════════════════════════════
    public async Task<int> SyncProductsAsync(Guid jobId, TenantId tenantId, Guid connectionId, int pageSize, int maxPages, CancellationToken ct)
    {
        var (shopName, accessToken, connId) = await ResolveConnectionAsync(tenantId, connectionId, ct);
        var cursorDate = await GetCursorOrNull(tenantId, connId, ProductCursorKey, ct);
        var sinceDate = cursorDate ?? DateTimeOffset.UtcNow.AddDays(-_opts.BackfillDays).ToString("o");

        const string query = @"
query($first: Int!, $after: String, $qf: String) {
  products(first: $first, after: $after, query: $qf, sortKey: UPDATED_AT) {
    pageInfo { hasNextPage endCursor }
    edges { node { id title description vendor productType createdAt updatedAt totalInventory
      variants(first: 100) { edges { node { id sku barcode price inventoryQuantity } } }
    }}
  }
}";
        var safeSize = Math.Min(pageSize <= 0 ? _opts.DefaultPageSize : pageSize, 250);
        var processed = 0; string? afterCursor = null;
        var maxSeen = cursorDate is not null ? DateTimeOffset.Parse(cursorDate) : DateTimeOffset.MinValue;
        for (var page = 0; page < maxPages; page++)
        {
            ct.ThrowIfCancellationRequested();
            using var doc = await _client.QueryAsync(shopName, accessToken, query, new { first = safeSize, after = afterCursor, qf = $"updated_at:>\"{sinceDate}\"" }, ct);
            ThrowOnErrors(doc);
            var products = doc.RootElement.GetProperty("data").GetProperty("products"); var edges = products.GetProperty("edges");
            if (edges.ValueKind != JsonValueKind.Array || edges.GetArrayLength() == 0) break;
            foreach (var edge in edges.EnumerateArray())
            {
                ct.ThrowIfCancellationRequested(); var n = edge.GetProperty("node");
                var pid = ExtractGid(Str(n, "id"));
                var pca = DateTimeOffset.TryParse(Str(n, "createdAt"), out var c) ? c : DateTimeOffset.UtcNow;
                var pua = DateTimeOffset.TryParse(Str(n, "updatedAt"), out var u) ? u : DateTimeOffset.UtcNow;
                if (pua > maxSeen) maxSeen = pua;
                var variants = new List<ShopifyProductVariantUpsert>();
                if (n.TryGetProperty("variants", out var vp) && vp.TryGetProperty("edges", out var ve) && ve.ValueKind == JsonValueKind.Array)
                    foreach (var v in ve.EnumerateArray()) { var vn = v.GetProperty("node"); variants.Add(new ShopifyProductVariantUpsert(ExtractGid(Str(vn, "id")), Str(vn, "sku"), Str(vn, "barcode"), DecStr(vn, "price"), (int?)Int64(vn, "inventoryQuantity"))); }
                await _store.UpsertProductAsync(tenantId, connId, new ShopifyProductUpsert(pid, Str(n, "title") ?? "unknown", Str(n, "description"), Str(n, "vendor"), Str(n, "productType"), (int)(Int64(n, "totalInventory") ?? 0), pca, pua, variants), ct);
                processed++;
            }
            await _jobs.MarkProgressAsync(jobId, processed, ct);
            var pi = products.GetProperty("pageInfo"); if (!Bool(pi, "hasNextPage")) break; afterCursor = Str(pi, "endCursor");
        }
        await SaveCursor(tenantId, connId, ProductCursorKey, maxSeen, ct);
        return processed;
    }

    // ═══════════════════════════════════════════════════════════
    private async Task<string?> GetCursorOrNull(TenantId t, ProviderConnectionId c, string k, CancellationToken ct) { var s = await _cursors.GetAsync(t, c, k, ct); return !string.IsNullOrWhiteSpace(s) && DateTimeOffset.TryParse(s, out _) ? s : null; }
    private async Task SaveCursor(TenantId t, ProviderConnectionId c, string k, DateTimeOffset ms, CancellationToken ct) { await _cursors.UpsertAsync(t, c, k, ms > DateTimeOffset.MinValue ? ms.ToString("o") : DateTimeOffset.UtcNow.ToString("o"), DateTimeOffset.UtcNow, ct); }
    private static void ThrowOnErrors(JsonDocument d) { if (d.RootElement.TryGetProperty("errors", out var e) && e.ValueKind == JsonValueKind.Array && e.GetArrayLength() > 0) { var m = e[0].TryGetProperty("message", out var msg) ? msg.GetString() : "Unknown"; throw new InvalidOperationException($"Shopify GraphQL: {m}"); } }
    private static string ExtractGid(string? g) { if (string.IsNullOrWhiteSpace(g)) return Guid.NewGuid().ToString("N"); var i = g.LastIndexOf('/'); return i >= 0 && i < g.Length - 1 ? g[(i + 1)..] : g; }
    private static string NormEmail(string? e) => (e ?? "").Trim().ToLowerInvariant();
    private static string NormPhone(string? p) { if (string.IsNullOrWhiteSpace(p)) return ""; var d = new string(p.Where(char.IsDigit).ToArray()); if (d.StartsWith("90")) return "+" + d; if (d.Length == 10) return "+90" + d; return d.Length > 0 ? "+" + d : ""; }
    private static string? Hash(string? v) => string.IsNullOrWhiteSpace(v) ? null : Sha256Hex(v);
    private static string Sha256Hex(string v) { var b = SHA256.HashData(Encoding.UTF8.GetBytes(v)); var sb = new StringBuilder(b.Length * 2); foreach (var x in b) sb.Append(x.ToString("x2")); return sb.ToString(); }
    private static string? Str(JsonElement o, string n) => o.TryGetProperty(n, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
    private static long? Int64(JsonElement o, string n) => o.TryGetProperty(n, out var p) && p.ValueKind == JsonValueKind.Number ? p.GetInt64() : null;
    private static bool Bool(JsonElement o, string n) => o.TryGetProperty(n, out var p) && p.ValueKind == JsonValueKind.True;
    private static decimal? DecStr(JsonElement o, string n) { if (!o.TryGetProperty(n, out var p)) return null; if (p.ValueKind == JsonValueKind.Number && p.TryGetDecimal(out var d)) return d; if (p.ValueKind == JsonValueKind.String && decimal.TryParse(p.GetString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var x)) return x; return null; }
}