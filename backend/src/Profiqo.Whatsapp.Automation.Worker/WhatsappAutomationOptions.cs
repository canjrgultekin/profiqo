namespace Profiqo.Whatsapp.Automation.Worker;

public sealed class WhatsappAutomationOptions
{
    public int SchedulerIntervalSeconds { get; init; } = 30;
    public int SenderPollMs { get; init; } = 500;

    public int LockTtlSeconds { get; init; } = 120;
    public int StaleSweepEverySeconds { get; init; } = 30;

    public int MaxAttempts { get; init; } = 10;
    public int BaseRetrySeconds { get; init; } = 3;
    public int MaxRetrySeconds { get; init; } = 300;
}