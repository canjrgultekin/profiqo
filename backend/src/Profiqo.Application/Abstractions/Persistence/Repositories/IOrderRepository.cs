using Profiqo.Domain.Common;
using Profiqo.Domain.Common.Ids;
using Profiqo.Domain.Orders;

namespace Profiqo.Application.Abstractions.Persistence.Repositories;

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(OrderId id, CancellationToken cancellationToken);

    Task<Order?> FindByProviderOrderIdAsync(
        TenantId tenantId,
        SalesChannel channel,
        string providerOrderId,
        CancellationToken cancellationToken);

    Task AddAsync(Order order, CancellationToken cancellationToken);
}