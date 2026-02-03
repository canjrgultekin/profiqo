namespace Profiqo.Application.Abstractions.Persistence.Whatsapp;

public interface IWhatsappSendJobRepository
{
    Task<Guid> CreateAsync(WhatsappSendJobCreateRequest req, CancellationToken ct);

    Task<WhatsappSendJobDto?> GetAsync(Guid jobId, CancellationToken ct);

    Task<WhatsappSendJobClaimDto?> TryClaimNextAsync(string workerId, DateTimeOffset nowUtc, CancellationToken ct);

    Task MarkSucceededAsync(Guid jobId, CancellationToken ct);

    Task MarkFailedAsync(Guid jobId, string error, CancellationToken ct);

    Task MarkRetryingAsync(Guid jobId, DateTimeOffset nextAttemptAtUtc, string error, CancellationToken ct);

    Task<int> ReleaseStaleLocksAsync(TimeSpan lockTtl, CancellationToken ct);
}