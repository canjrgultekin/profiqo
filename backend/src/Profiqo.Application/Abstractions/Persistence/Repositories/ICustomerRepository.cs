// Path: backend/src/Profiqo.Application/Abstractions/Persistence/Repositories/ICustomerRepository.cs
using Profiqo.Domain.Common.Ids;
using Profiqo.Domain.Customers;

namespace Profiqo.Application.Abstractions.Persistence.Repositories;

public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(CustomerId id, CancellationToken cancellationToken);


    // ✅ NEW: Customer entity materialize etmeden sadece id bul (nullable materialization hatasını bypass eder)
    Task<CustomerId?> FindIdByIdentityHashAsync(
        TenantId tenantId,
        IdentityType identityType,
        IdentityHash valueHash,
        CancellationToken cancellationToken);

    Task AddAsync(Customer customer, CancellationToken cancellationToken);
}