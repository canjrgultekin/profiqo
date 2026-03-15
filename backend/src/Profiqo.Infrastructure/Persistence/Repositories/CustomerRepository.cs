// Path: backend/src/Profiqo.Infrastructure/Persistence/Repositories/CustomerRepository.cs
using Microsoft.EntityFrameworkCore;

using Profiqo.Application.Abstractions.Persistence.Repositories;
using Profiqo.Domain.Common.Ids;
using Profiqo.Domain.Customers;

namespace Profiqo.Infrastructure.Persistence.Repositories;

public sealed class CustomerRepository : ICustomerRepository
{
    private readonly ProfiqoDbContext _db;

    public CustomerRepository(ProfiqoDbContext db)
    {
        _db = db;
    }

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

    // Identity resolution provider-agnostic olmalı.
    // Unique constraint (tenant_id, type, value_hash) üzerinde, provider fark etmez.
    // Bir müşterinin email hash'i aynı hash, Ikas'tan gelsin Pixel'den gelsin.
    public async Task<CustomerId?> FindIdByIdentityHashAsync(
        TenantId tenantId,
        IdentityType identityType,
        IdentityHash valueHash,
        CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT i.customer_id AS ""Value""
FROM public.customer_identities i
WHERE i.tenant_id = {0} AND i.type = {1} AND i.value_hash = {2}
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