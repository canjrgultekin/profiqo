using Microsoft.EntityFrameworkCore;

using Profiqo.Application.Abstractions.Persistence.Repositories;
using Profiqo.Domain.Common;
using Profiqo.Domain.Common.Ids;
using Profiqo.Domain.Orders;

namespace Profiqo.Infrastructure.Persistence.Repositories;

internal sealed class OrderRepository : IOrderRepository
{
    private readonly ProfiqoDbContext _db;

    public OrderRepository(ProfiqoDbContext db)
    {
        _db = db;
    }

    public Task<Order?> GetByIdAsync(OrderId id, CancellationToken cancellationToken)
        => _db.Orders.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<Order?> FindByProviderOrderIdAsync(
        TenantId tenantId,
        SalesChannel channel,
        string providerOrderId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(providerOrderId))
            throw new ArgumentException("providerOrderId is required.", nameof(providerOrderId));

        return _db.Orders
            .Where(o => o.TenantId == tenantId && o.Channel == channel && o.ProviderOrderId == providerOrderId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AddAsync(Order order, CancellationToken cancellationToken)
    {
        await _db.Orders.AddAsync(order, cancellationToken);
    }
}