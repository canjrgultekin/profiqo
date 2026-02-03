namespace Profiqo.Application.Abstractions.Persistence.WhatsappAutomation;

public interface IWhatsappDispatchRepository
{
    Task<IReadOnlyList<WhatsappDispatchDto>> ListRecentAsync(Guid tenantId, int take, CancellationToken ct);

    Task<Guid> EnqueueManualAsync(
        Guid tenantId,
        Guid jobId,
        Guid ruleId,
        Guid customerId,
        string toE164,
        short messageNo,
        Guid templateId,
        DateTimeOffset plannedAtUtc,
        DateOnly localDate,
        string payloadJson,
        CancellationToken ct);

    Task<Guid?> TryEnqueueUniqueAsync(
        Guid tenantId,
        Guid jobId,
        Guid ruleId,
        Guid customerId,
        string toE164,
        short messageNo,
        Guid templateId,
        DateTimeOffset plannedAtUtc,
        DateOnly localDate,
        string payloadJson,
        CancellationToken ct);

    Task<(Guid Id, Guid TenantId, Guid JobId, Guid RuleId, Guid CustomerId, string ToE164, short MessageNo, Guid TemplateId, DateTimeOffset PlannedAtUtc, DateOnly LocalDate, string PayloadJson, int AttemptCount)?>
        TryClaimNextAsync(string workerId, DateTimeOffset nowUtc, CancellationToken ct);
    Task MarkSentDummyAsync(Guid id, CancellationToken ct);

    Task MarkSentAsync(Guid id, bool isSimulated, CancellationToken ct);
    Task MarkSuppressedLimitAsync(Guid id, string reason, CancellationToken ct);
    Task MarkFailedAsync(Guid id, string error, DateTimeOffset nextAttemptAtUtc, CancellationToken ct);

    Task<bool> TryConsumeDailyQuotaAsync(Guid tenantId, Guid customerId, DateOnly localDate, short limit, CancellationToken ct);

    Task<int> ReleaseStaleLocksAsync(TimeSpan lockTtl, CancellationToken ct);
}