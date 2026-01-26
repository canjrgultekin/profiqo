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
        string valueHash,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(valueHash))
            throw new ArgumentException("valueHash is required.", nameof(valueHash));

        // Tenant filter is already on, but we still match tenant explicitly for clarity and safety.
        var hash = new IdentityHash(valueHash);

        return await _db.Customers
            .Where(c => c.TenantId == tenantId)
            .Where(c => c.Identities.Any(i => i.Type == identityType && i.ValueHash == hash))
            .FirstOrDefaultAsync(cancellationToken);

    }

    public async Task AddAsync(Customer customer, CancellationToken cancellationToken)
    {
        await _db.Customers.AddAsync(customer, cancellationToken);
    }
}