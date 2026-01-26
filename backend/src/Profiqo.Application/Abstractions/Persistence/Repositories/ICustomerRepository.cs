using Profiqo.Domain.Common;
using Profiqo.Domain.Common.Ids;
using Profiqo.Domain.Customers;

namespace Profiqo.Application.Abstractions.Persistence.Repositories;

public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(CustomerId id, CancellationToken cancellationToken);

    Task<Customer?> FindByIdentityHashAsync(
        TenantId tenantId,
        IdentityType identityType,
        string valueHash,
        CancellationToken cancellationToken);


    Task AddAsync(Customer customer, CancellationToken cancellationToken);
}