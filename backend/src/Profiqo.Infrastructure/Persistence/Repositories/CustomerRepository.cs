using Microsoft.EntityFrameworkCore;
using System.Linq;
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

    public Task<Customer?> GetByIdAsync(CustomerId id, CancellationToken cancellationToken)
        => _db.Customers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<Customer?> FindByIdentityHashAsync(
        TenantId tenantId,
        IdentityType identityType,
        IdentityHash valueHash,
        CancellationToken ct)
    {
        return await _db.Customers
            .AsTracking()
            .Where(c => c.TenantId == tenantId)
            .Where(c => c.Identities.Any(i => i.Type == identityType && i.ValueHash == valueHash))
            .FirstOrDefaultAsync(ct);
    }
    public async Task AddAsync(Customer customer, CancellationToken cancellationToken)
    {
        await _db.Customers.AddAsync(customer, cancellationToken);
    }
}