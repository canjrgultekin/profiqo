using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Profiqo.Application.Customers.Dedupe;
using Profiqo.Domain.Common.Ids;
using Profiqo.Infrastructure.Persistence;
using Profiqo.Infrastructure.Persistence.Entities;
using Profiqo.Infrastructure.Persistence.QueryTypes;

namespace Profiqo.Infrastructure.Persistence.Repositories;

internal sealed class CustomerDedupeAnalysisRepository : ICustomerDedupeAnalysisRepository
{
    private readonly ProfiqoDbContext _db;

    public CustomerDedupeAnalysisRepository(ProfiqoDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<CustomerDedupeRow>> GetCustomerRowsAsync(TenantId tenantId, CancellationToken ct)
    {
        // ✅ Approved merge sonrası source customer’ları analiz dataset’inden çıkar
        var rows = await (
            from c in _db.Customers.AsNoTracking()
            where c.TenantId == tenantId
            join ml in _db.Set<CustomerMergeLink>().AsNoTracking().Where(x => x.TenantId == tenantId)
                on c.Id equals ml.SourceCustomerId into mlj
            from ml in mlj.DefaultIfEmpty()
            where ml == null
            select new CustomerDedupeRow(
                CustomerId: c.Id.Value,
                FirstName: c.FirstName,
                LastName: c.LastName)
        ).ToListAsync(ct);

        return rows;
    }

    public async Task<IReadOnlyList<CustomerOrderAggRow>> GetOrderAggsAsync(TenantId tenantId, CancellationToken ct)
    {
        const string sql = @"
SELECT
  o.customer_id        AS customer_id,
  o.channel            AS channel,
  COUNT(*)::int        AS orders_count,
  COALESCE(SUM(o.total_amount), 0)::numeric AS total_amount,
  o.total_currency     AS currency
FROM public.orders o
WHERE o.tenant_id = {0}
GROUP BY o.customer_id, o.channel, o.total_currency;
";

        var rows = await _db.Set<CustomerOrderAggRowDb>()
            .FromSqlRaw(sql, tenantId.Value)
            .AsNoTracking()
            .ToListAsync(ct);

        return rows.Select(x => new CustomerOrderAggRow(
            CustomerId: x.customer_id,
            Channel: x.channel,
            OrdersCount: x.orders_count,
            TotalAmount: x.total_amount,
            Currency: x.currency)).ToList();
    }

    public async Task<IReadOnlyDictionary<Guid, AddressPairRow>> GetLatestAddressPairsAsync(TenantId tenantId, CancellationToken ct)
    {
        var rows = await _db.Orders.AsNoTracking()
            .Where(o => o.TenantId == tenantId)
            .OrderByDescending(o => o.PlacedAtUtc)
            .Select(o => new
            {
                CustomerId = o.CustomerId.Value,
                Ship = EF.Property<string?>(o, "ShippingAddressJson"),
                Bill = EF.Property<string?>(o, "BillingAddressJson"),
            })
            .ToListAsync(ct);

        var dict = new Dictionary<Guid, AddressPairRow>();

        foreach (var r in rows)
        {
            if (dict.ContainsKey(r.CustomerId))
                continue;

            dict[r.CustomerId] = new AddressPairRow(
                Shipping: ParseAddress(r.Ship),
                Billing: ParseAddress(r.Bill));
        }

        return dict;
    }

    private static AddressSnapshotDto? ParseAddress(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            string? country = TryGetString(root, "country");
            string? city = TryGetString(root, "city");
            string? district = TryGetString(root, "district");
            string? postal = TryGetString(root, "postalCode");
            string? a1 = TryGetString(root, "addressLine1");
            string? a2 = TryGetString(root, "addressLine2");
            string? fullName = TryGetString(root, "fullName");
            string? phone = TryGetString(root, "phone");

            if (city is null && root.TryGetProperty("city", out var c) && c.ValueKind == JsonValueKind.Object)
                city = TryGetString(c, "name");

            if (district is null && root.TryGetProperty("district", out var d) && d.ValueKind == JsonValueKind.Object)
                district = TryGetString(d, "name");

            if (country is null && root.TryGetProperty("country", out var co) && co.ValueKind == JsonValueKind.Object)
                country = TryGetString(co, "code") ?? TryGetString(co, "name");

            postal ??= TryGetString(root, "postalCode");

            var dto = new AddressSnapshotDto(country, city, district, postal, a1, a2, fullName) { Phone = phone };

            return dto;
        }
        catch
        {
            return new AddressSnapshotDto(null, null, null, null, null, null, null) { Phone = null };
        }
    }

    private static string? TryGetString(JsonElement obj, string name)
        => obj.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
}
