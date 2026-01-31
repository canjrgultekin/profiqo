// Path: backend/src/Profiqo.Application/Customers/Dedupe/ICustomerDedupeAnalysisRepository.cs
using Profiqo.Domain.Common.Ids;

namespace Profiqo.Application.Customers.Dedupe;

public interface ICustomerDedupeAnalysisRepository
{
    Task<IReadOnlyList<CustomerDedupeRow>> GetCustomerRowsAsync(TenantId tenantId, CancellationToken ct);

    Task<IReadOnlyList<CustomerOrderAggRow>> GetOrderAggsAsync(TenantId tenantId, CancellationToken ct);

    Task<IReadOnlyDictionary<Guid, AddressPairRow>> GetLatestAddressPairsAsync(TenantId tenantId, CancellationToken ct);
}

public sealed record CustomerDedupeRow(
    Guid CustomerId,
    string? FirstName,
    string? LastName);

public sealed record CustomerOrderAggRow(
    Guid CustomerId,
    short Channel,
    int OrdersCount,
    decimal TotalAmount,
    string Currency);

public sealed record AddressPairRow(
    AddressSnapshotDto? Shipping,
    AddressSnapshotDto? Billing);