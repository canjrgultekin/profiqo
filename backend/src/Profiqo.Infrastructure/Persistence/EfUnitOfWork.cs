using Profiqo.Application.Abstractions.Persistence;

namespace Profiqo.Infrastructure.Persistence;

internal sealed class EfUnitOfWork : IUnitOfWork
{
    private readonly ProfiqoDbContext _db;

    public EfUnitOfWork(ProfiqoDbContext db)
    {
        _db = db;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        => _db.SaveChangesAsync(cancellationToken);
}