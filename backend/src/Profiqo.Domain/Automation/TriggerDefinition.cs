using Profiqo.Domain.Common;

namespace Profiqo.Domain.Automation;


public sealed class TriggerDefinition
{
    public TriggerType Type { get; private set; }

    public string? EventType { get; private set; }
    public string? CronExpression { get; private set; }

    public string? ScoreField { get; private set; }
    public RuleOperator? ScoreOperator { get; private set; }
    public string? ScoreValueJson { get; private set; }

    private TriggerDefinition()
    {
    }

    private TriggerDefinition(
        TriggerType type,
        string? eventType,
        string? cronExpression,
        string? scoreField,
        RuleOperator? scoreOperator,
        string? scoreValueJson)
    {
        Type = type;

        EventType = eventType is null ? null : Guard.AgainstTooLong(Guard.AgainstNullOrWhiteSpace(eventType, nameof(eventType)), 100, nameof(eventType));
        CronExpression = cronExpression is null ? null : Guard.AgainstTooLong(Guard.AgainstNullOrWhiteSpace(cronExpression, nameof(cronExpression)), 200, nameof(cronExpression));

        ScoreField = scoreField is null ? null : Guard.AgainstTooLong(Guard.AgainstNullOrWhiteSpace(scoreField, nameof(scoreField)), 200, nameof(scoreField));
        ScoreOperator = scoreOperator;
        ScoreValueJson = scoreValueJson is null ? null : Guard.AgainstTooLong(Guard.AgainstNullOrWhiteSpace(scoreValueJson, nameof(scoreValueJson)), 2048, nameof(scoreValueJson));
    }

    public static TriggerDefinition ForEvent(string eventType)
        => new(TriggerType.Event, eventType, null, null, null, null);

    public static TriggerDefinition ForSchedule(string cronExpression)
        => new(TriggerType.Schedule, null, cronExpression, null, null, null);

    public static TriggerDefinition ForScoreChange(string scoreField, RuleOperator op, string scoreValueJson)
        => new(TriggerType.ScoreChange, null, null, scoreField, op, scoreValueJson);
}