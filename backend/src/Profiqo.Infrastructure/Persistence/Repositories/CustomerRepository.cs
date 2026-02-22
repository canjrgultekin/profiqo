// Path: backend/src/Profiqo.Infrastructure/Persistence/Repositories/CustomerRepository.cs
using Microsoft.EntityFrameworkCore;

using Profiqo.Application.Abstractions.Persistence.Repositories;
using Profiqo.Domain.Common.Ids;
using Profiqo.Domain.Customers;

namespace Profiqo.Infrastructure.Persistence.Repositories;

internal sealed class CustomerRepository : ICustomerRepository
{
    private readonly ProfiqoDbContext _db;

    public CustomerRepository(ProfiqoDbContext db)
    {
        _db = db;
    }

    // ✅ Poison customer row varsa materialization patlayabilir, sistemi düşürmeyelim.
    public async Task<Customer?> GetByIdAsync(CustomerId id, CancellationToken cancellationToken)
    {
        try
        {
            return await _db.Customers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    // ✅ Interface’te var, safe implement
    public async Task<Customer?> FindByIdentityHashAsync(
        TenantId tenantId,
        IdentityType identityType,
        IdentityHash valueHash,
        CancellationToken cancellationToken)
    {
        var id = await FindIdByIdentityHashAsync(tenantId, identityType, valueHash, cancellationToken);
        if (id is null) return null;

        try
        {
            // Tenant filtreli + tracked
            return await _db.Customers
                .AsTracking()
                .Where(c => c.TenantId == tenantId && c.Id == id.Value)
                .FirstOrDefaultAsync(cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    // ✅ NEW: entity materialize etmeden yalnızca customer_id çekiyoruz.
    // Not: EF scalar query’yi SELECT s."Value" ile sarar, o yüzden alias zorunlu.
    public async Task<CustomerId?> FindIdByIdentityHashAsync(
        TenantId tenantId,
        IdentityType identityType,
        IdentityHash valueHash,
        CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT i.customer_id AS ""Value""
FROM public.customer_identities i
WHERE i.tenant_id = {0} AND i.type = {1} AND i.value_hash = {2} AND i.source_provider = 1
LIMIT 1";

        var tenantGuid = tenantId.Value;
        var type = (short)identityType;
        var hash = valueHash.Value;

        var guid = await _db.Database
            .SqlQueryRaw<Guid?>(sql, tenantGuid, type, hash)
            .FirstOrDefaultAsync(cancellationToken);

        return guid is null ? null : new CustomerId(guid.Value);
    }

    public async Task AddAsync(Customer customer, CancellationToken cancellationToken)
    {
        await _db.Customers.AddAsync(customer, cancellationToken);
    }
}