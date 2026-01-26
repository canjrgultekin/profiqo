using Profiqo.Domain.Common;
using Profiqo.Domain.Common.Ids;

namespace Profiqo.Domain.Messaging;


public sealed class MessageTemplate : AggregateRoot<MessageTemplateId>
{
    private static readonly Regex PlaceholderRegex = new(@"\{\{\s*([a-zA-Z0-9_.]+)\s*\}\}", RegexOptions.Compiled);

    public TenantId TenantId { get; private set; }

    public string Name { get; private set; }
    public MessageChannel Channel { get; private set; }
    public string Language { get; private set; }

    public string Body { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    private MessageTemplate() : base()
    {
        TenantId = default;
        Name = string.Empty;
        Language = "tr";
        Body = string.Empty;
    }

    private MessageTemplate(
        MessageTemplateId id,
        TenantId tenantId,
        string name,
        MessageChannel channel,
        string language,
        string body,
        DateTimeOffset nowUtc) : base(id)
    {
        TenantId = tenantId;

        Name = Guard.AgainstTooLong(Guard.AgainstNullOrWhiteSpace(name, nameof(name)), 200, nameof(name));
        Channel = channel;
        Language = Guard.AgainstTooLong(Guard.AgainstNullOrWhiteSpace(language, nameof(language)), 16, nameof(language)).ToLowerInvariant();

        Body = Guard.AgainstTooLong(Guard.AgainstNullOrWhiteSpace(body, nameof(body)), 8000, nameof(body));

        CreatedAtUtc = Guard.EnsureUtc(nowUtc, nameof(nowUtc));
        UpdatedAtUtc = CreatedAtUtc;

        ValidatePlaceholders();
    }

    public static MessageTemplate Create(
        TenantId tenantId,
        string name,
        MessageChannel channel,
        string language,
        string body,
        DateTimeOffset nowUtc)
    {
        return new MessageTemplate(MessageTemplateId.New(), tenantId, name, channel, language, body, nowUtc);
    }

    public void UpdateBody(string body, DateTimeOffset nowUtc)
    {
        Body = Guard.AgainstTooLong(Guard.AgainstNullOrWhiteSpace(body, nameof(body)), 8000, nameof(body));
        ValidatePlaceholders();

        UpdatedAtUtc = Guard.EnsureUtc(nowUtc, nameof(nowUtc));
    }

    public IReadOnlyCollection<string> GetPlaceholders()
    {
        var matches = PlaceholderRegex.Matches(Body);
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match m in matches)
        {
            var key = m.Groups[1].Value;
            if (!string.IsNullOrWhiteSpace(key))
                set.Add(key);
        }

        return set.ToList().AsReadOnly();
    }

    private void ValidatePlaceholders()
    {
        foreach (var placeholder in GetPlaceholders())
        {
            if (placeholder.Length > 100)
                throw new BusinessRuleViolationException("template_placeholder_too_long", "Template placeholder is too long.");

            if (placeholder.Contains("..", StringComparison.Ordinal))
                throw new BusinessRuleViolationException("template_placeholder_invalid", "Template placeholder is invalid.");
        }
    }
}
