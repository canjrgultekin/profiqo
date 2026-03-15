using Profiqo.Domain.Common.Ids;
using Profiqo.Domain.Users;

namespace Profiqo.Application.Abstractions.Persistence.Repositories;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(UserId id, CancellationToken cancellationToken);
    Task<User?> GetByEmailAsync(TenantId tenantId, EmailAddress email, CancellationToken cancellationToken);
    Task AddAsync(User user, CancellationToken cancellationToken);
}