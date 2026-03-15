using System.Collections.ObjectModel;
using System.Text.Json;

using Profiqo.Domain.Common;
using Profiqo.Domain.Common.Ids;
using Profiqo.Domain.Messaging;

namespace Profiqo.Domain.Automation;

public sealed class RuleAction
{
    public ActionType Type { get; private set; }

    public MessageChannel? Channel { get; private set; }
    public MessageTemplateId? TemplateId { get; private set; }

    // Domain-side usable object
    public IReadOnlyDictionary<string, string>? Personalization { get; private set; }

    // Persistence-only (EF mapped)
    public string? PersonalizationJson { get; private set; }

    public string? Tag { get; private set; }
    public string? SegmentId { get; private set; }

    public string? TaskAssignee { get; private set; }
    public string? TaskDescription { get; private set; }

    // EF Core ONLY
    private RuleAction()
    {
    }

    private RuleAction(
        ActionType type,
        MessageChannel? channel,
        MessageTemplateId? templateId,
        IReadOnlyDictionary<string, string>? personalization,
        string? personalizationJson,
        string? tag,
        string? segmentId,
        string? taskAssignee,
        string? taskDescription)
    {
        Type = type;
        Channel = channel;
        TemplateId = templateId;
        Personalization = personalization;
        PersonalizationJson = personalizationJson;
        Tag = tag;
        SegmentId = segmentId;
        TaskAssignee = taskAssignee;
        TaskDescription = taskDescription;

        ValidateInvariants();
    }

    /* ---------------------- FACTORIES ---------------------- */

    public static RuleAction SendMessage(
        MessageChannel channel,
        MessageTemplateId templateId,
        IDictionary<string, string>? personalization)
    {
        IReadOnlyDictionary<string, string>? p = null;
        string? json = null;

        if (personalization is not null && personalization.Count > 0)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var kv in personalization)
            {
                var key = Guard.AgainstTooLong(
                    Guard.AgainstNullOrWhiteSpace(kv.Key, nameof(kv.Key)),
                    100,
                    nameof(kv.Key));

                var value = Guard.AgainstTooLong(
                    Guard.AgainstNullOrWhiteSpace(kv.Value, nameof(kv.Value)),
                    200,
                    nameof(kv.Value));

                dict[key] = value;
            }

            p = new ReadOnlyDictionary<string, string>(dict);
            json = JsonSerializer.Serialize(dict);
        }

        return new RuleAction(
            ActionType.SendMessage,
            channel,
            templateId,
            p,
            json,
            null,
            null,
            null,
            null);
    }

    public static RuleAction AddTag(string tag)
        => new(
            ActionType.AddTag,
            null,
            null,
            null,
            null,
            Guard.AgainstTooLong(
                Guard.AgainstNullOrWhiteSpace(tag, nameof(tag)),
                80,
                nameof(tag)),
            null,
            null,
            null);

    public static RuleAction AssignToSegment(string segmentId)
        => new(
            ActionType.AssignToSegment,
            null,
            null,
            null,
            null,
            null,
            Guard.AgainstTooLong(
                Guard.AgainstNullOrWhiteSpace(segmentId, nameof(segmentId)),
                80,
                nameof(segmentId)),
            null,
            null);

    public static RuleAction CreateTask(string assignee, string description)
    {
        assignee = Guard.AgainstTooLong(
            Guard.AgainstNullOrWhiteSpace(assignee, nameof(assignee)),
            200,
            nameof(assignee));

        description = Guard.AgainstTooLong(
            Guard.AgainstNullOrWhiteSpace(description, nameof(description)),
            2000,
            nameof(description));

        return new RuleAction(
            ActionType.CreateTask,
            null,
            null,
            null,
            null,
            null,
            null,
            assignee,
            description);
    }

    /* ---------------------- INTERNAL ---------------------- */

    // EF Core materialization sonrası çağrılabilir
    internal void Rehydrate()
    {
        if (Personalization is null && !string.IsNullOrWhiteSpace(PersonalizationJson))
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(PersonalizationJson);
            if (dict is not null)
                Personalization = new ReadOnlyDictionary<string, string>(dict);
        }

        ValidateInvariants();
    }

    private void ValidateInvariants()
    {
        switch (Type)
        {
            case ActionType.SendMessage:
                if (Channel is null)
                    throw new DomainException("SendMessage action requires Channel.");

                if (TemplateId is null)
                    throw new DomainException("SendMessage action requires TemplateId.");
                break;

            case ActionType.AddTag:
                if (string.IsNullOrWhiteSpace(Tag))
                    throw new DomainException("AddTag action requires Tag.");
                break;

            case ActionType.AssignToSegment:
                if (string.IsNullOrWhiteSpace(SegmentId))
                    throw new DomainException("AssignToSegment action requires SegmentId.");
                break;

            case ActionType.CreateTask:
                if (string.IsNullOrWhiteSpace(TaskAssignee) || string.IsNullOrWhiteSpace(TaskDescription))
                    throw new DomainException("CreateTask action requires assignee and description.");
                break;
        }
    }
}
