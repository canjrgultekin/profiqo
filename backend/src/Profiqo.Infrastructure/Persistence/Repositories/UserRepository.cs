using Microsoft.EntityFrameworkCore;

using Profiqo.Application.Abstractions.Persistence.Repositories;
using Profiqo.Domain.Common.Ids;
using Profiqo.Domain.Users;

namespace Profiqo.Infrastructure.Persistence.Repositories;

internal sealed class UserRepository : IUserRepository
{
    private readonly ProfiqoDbContext _db;

    public UserRepository(ProfiqoDbContext db)
    {
        _db = db;
    }

    public Task<User?> GetByIdAsync(UserId id, CancellationToken cancellationToken)
        => _db.Set<User>().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<User?> GetByEmailAsync(TenantId tenantId, EmailAddress email, CancellationToken cancellationToken)
        => _db.Set<User>().FirstOrDefaultAsync(
            x => x.TenantId == tenantId && x.Email == email,
            cancellationToken);

    public async Task AddAsync(User user, CancellationToken cancellationToken)
    {
        await _db.Set<User>().AddAsync(user, cancellationToken);
    }
}