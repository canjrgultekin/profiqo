// Path: backend/src/Profiqo.Application/Customers/Dedupe/AnalyzeCustomerDuplicatesQuery.cs
using MediatR;

namespace Profiqo.Application.Customers.Dedupe;

public sealed record AnalyzeCustomerDuplicatesQuery(double? Threshold = null) : IRequest<AnalyzeCustomerDuplicatesResultDto>;

public sealed record AnalyzeCustomerDuplicatesResultDto(
    IReadOnlyList<CustomerDuplicateGroupDto> Groups);

public sealed record CustomerDuplicateGroupDto(
    string GroupKey,
    double Confidence,
    string NormalizedName,
    IReadOnlyList<CustomerDuplicateCandidateDto> Candidates,
    string Rationale);

public sealed record CustomerDuplicateCandidateDto(
    Guid CustomerId,
    string? FirstName,
    string? LastName,
    IReadOnlyList<CustomerChannelSummaryDto> Channels,
    AddressSnapshotDto? ShippingAddress,
    AddressSnapshotDto? BillingAddress);

public sealed record CustomerChannelSummaryDto(
    string Channel,
    int OrdersCount,
    decimal TotalAmount,
    string Currency);

public sealed record AddressSnapshotDto(
    string? Country,
    string? City,
    string? District,
    string? PostalCode,
    string? AddressLine1,
    string? AddressLine2,
    string? FullName)
{
    public string? Phone { get; init; }
}