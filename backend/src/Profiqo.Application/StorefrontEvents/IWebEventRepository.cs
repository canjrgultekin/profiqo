using Profiqo.Domain.Common.Ids;

namespace Profiqo.Application.StorefrontEvents;

public interface IWebEventRepository
{
    Task InsertBatchAsync(
        IReadOnlyList<WebEventInsert> events,
        CancellationToken ct);
}

public sealed record WebEventInsert
{
    public required Guid Id { get; init; }
    public required Guid TenantId { get; init; }
    public required string EventType { get; init; }
    public required string DeviceIdHash { get; init; }
    public string? SessionId { get; init; }
    public Guid? CustomerId { get; init; }
    public string? ClientIp { get; init; }
    public string? PageUrl { get; init; }
    public string? PagePath { get; init; }
    public string? PageReferrer { get; init; }
    public string? PageTitle { get; init; }
    public string? UserAgent { get; init; }
    public required string EventDataJson { get; init; }
    public required DateTimeOffset OccurredAtUtc { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
}
