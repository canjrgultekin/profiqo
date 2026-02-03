namespace Profiqo.Whatsapp.Worker;

public sealed class WhatsappSendWorkerOptions
{
    public int PollIntervalMs { get; init; } = 500;
    public int MaxAttempts { get; init; } = 12;

    public int LockTtlSeconds { get; init; } = 120;
    public int StaleSweepEverySeconds { get; init; } = 30;

    public int BaseRetrySeconds { get; init; } = 5;
    public int MaxRetrySeconds { get; init; } = 900;
}