using Profiqo.Application.Abstractions.Persistence.Whatsapp;

namespace Profiqo.Infrastructure.Persistence.Entities;

public sealed class WhatsappSendJob
{
    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }
    public Guid ConnectionId { get; private set; }

    public WhatsappSendJobStatus Status { get; private set; }

    public int AttemptCount { get; private set; }
    public DateTimeOffset NextAttemptAtUtc { get; private set; }

    public string PayloadJson { get; private set; }

    public string? LockedBy { get; private set; }
    public DateTimeOffset? LockedAtUtc { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public DateTimeOffset? StartedAtUtc { get; private set; }
    public DateTimeOffset? FinishedAtUtc { get; private set; }

    public string? LastError { get; private set; }

    private WhatsappSendJob()
    {
        PayloadJson = "{}";
    }

    public WhatsappSendJob(Guid id, Guid tenantId, Guid connectionId, string payloadJson, DateTimeOffset nextAttemptAtUtc)
    {
        Id = id;
        TenantId = tenantId;
        ConnectionId = connectionId;
        PayloadJson = payloadJson;
        NextAttemptAtUtc = nextAttemptAtUtc.ToUniversalTime();

        Status = WhatsappSendJobStatus.Queued;
        AttemptCount = 0;
        CreatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
    }

    public void MarkRunning(string workerId)
    {
        Status = WhatsappSendJobStatus.Running;
        LockedBy = workerId;
        LockedAtUtc = DateTimeOffset.UtcNow;
        StartedAtUtc ??= DateTimeOffset.UtcNow;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void MarkSucceeded()
    {
        Status = WhatsappSendJobStatus.Succeeded;
        FinishedAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        LastError = null;
        LockedBy = null;
        LockedAtUtc = null;
    }

    public void MarkRetrying(DateTimeOffset nextAttemptAtUtc, string error)
    {
        Status = WhatsappSendJobStatus.Retrying;
        AttemptCount = Math.Min(AttemptCount + 1, 1_000_000);
        NextAttemptAtUtc = nextAttemptAtUtc.ToUniversalTime();
        LastError = error;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        LockedBy = null;
        LockedAtUtc = null;
    }

    public void MarkFailed(string error)
    {
        Status = WhatsappSendJobStatus.Failed;
        AttemptCount = Math.Min(AttemptCount + 1, 1_000_000);
        FinishedAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        LastError = error;
        LockedBy = null;
        LockedAtUtc = null;
    }

    public void ForceReleaseLock()
    {
        LockedBy = null;
        LockedAtUtc = null;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}
