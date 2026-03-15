using System.Text.Json;

using Profiqo.Domain.Common;
using Profiqo.Domain.Common.Ids;
using Profiqo.Domain.Integrations;

namespace Profiqo.Domain.Integrations.Whatsapp;

public sealed class WhatsappTemplate : AggregateRoot<WhatsappTemplateId>
{
    public TenantId TenantId { get; private set; }
    public ProviderConnectionId ConnectionId { get; private set; }

    public string Name { get; private set; }
    public string Language { get; private set; }
    public string Category { get; private set; }

    public string Status { get; private set; }
    public string ComponentsJson { get; private set; }

    public string? MetaTemplateId { get; private set; }
    public string? RejectionReason { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    private WhatsappTemplate() : base()
    {
        TenantId = default;
        ConnectionId = default;
        Name = string.Empty;
        Language = "tr";
        Category = "MARKETING";
        Status = "PENDING";
        ComponentsJson = "[]";
    }

    private WhatsappTemplate(
        WhatsappTemplateId id,
        TenantId tenantId,
        ProviderConnectionId connectionId,
        string name,
        string language,
        string category,
        string status,
        string componentsJson,
        string? metaTemplateId,
        string? rejectionReason,
        DateTimeOffset nowUtc) : base(id)
    {
        TenantId = tenantId;
        ConnectionId = connectionId;

        Name = Guard.AgainstTooLong(Guard.AgainstNullOrWhiteSpace(name, nameof(name)), 512, nameof(name));
        Language = Guard.AgainstTooLong(Guard.AgainstNullOrWhiteSpace(language, nameof(language)), 16, nameof(language)).ToLowerInvariant();
        Category = Guard.AgainstTooLong(Guard.AgainstNullOrWhiteSpace(category, nameof(category)), 32, nameof(category)).ToUpperInvariant();

        Status = Guard.AgainstTooLong(Guard.AgainstNullOrWhiteSpace(status, nameof(status)), 32, nameof(status)).ToUpperInvariant();
        ComponentsJson = ValidateComponentsJson(componentsJson);

        MetaTemplateId = string.IsNullOrWhiteSpace(metaTemplateId) ? null : Guard.AgainstTooLong(metaTemplateId.Trim(), 200, nameof(metaTemplateId));
        RejectionReason = string.IsNullOrWhiteSpace(rejectionReason) ? null : Guard.AgainstTooLong(rejectionReason.Trim(), 2000, nameof(rejectionReason));

        CreatedAtUtc = Guard.EnsureUtc(nowUtc, nameof(nowUtc));
        UpdatedAtUtc = CreatedAtUtc;
    }

    public static WhatsappTemplate Create(
        TenantId tenantId,
        ProviderConnectionId connectionId,
        string name,
        string language,
        string category,
        string status,
        string componentsJson,
        string? metaTemplateId,
        string? rejectionReason,
        DateTimeOffset nowUtc)
    {
        return new WhatsappTemplate(
            WhatsappTemplateId.New(),
            tenantId,
            connectionId,
            name,
            language,
            category,
            status,
            componentsJson,
            metaTemplateId,
            rejectionReason,
            nowUtc);
    }

    public void UpdateComponents(string language, string category, string status, string componentsJson, DateTimeOffset nowUtc)
    {
        Language = Guard.AgainstTooLong(Guard.AgainstNullOrWhiteSpace(language, nameof(language)), 16, nameof(language)).ToLowerInvariant();
        Category = Guard.AgainstTooLong(Guard.AgainstNullOrWhiteSpace(category, nameof(category)), 32, nameof(category)).ToUpperInvariant();
        Status = Guard.AgainstTooLong(Guard.AgainstNullOrWhiteSpace(status, nameof(status)), 32, nameof(status)).ToUpperInvariant();
        ComponentsJson = ValidateComponentsJson(componentsJson);
        Touch(nowUtc);
    }

    public void UpdateMetaStatus(string status, string? rejectionReason, string? metaTemplateId, DateTimeOffset nowUtc)
    {
        Status = Guard.AgainstTooLong(Guard.AgainstNullOrWhiteSpace(status, nameof(status)), 32, nameof(status)).ToUpperInvariant();

        if (!string.IsNullOrWhiteSpace(metaTemplateId))
            MetaTemplateId = Guard.AgainstTooLong(metaTemplateId.Trim(), 200, nameof(metaTemplateId));

        RejectionReason = string.IsNullOrWhiteSpace(rejectionReason) ? null : Guard.AgainstTooLong(rejectionReason.Trim(), 2000, nameof(rejectionReason));
        Touch(nowUtc);
    }

    private void Touch(DateTimeOffset nowUtc)
        => UpdatedAtUtc = Guard.EnsureUtc(nowUtc, nameof(nowUtc));

    private static string ValidateComponentsJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new DomainException("ComponentsJson cannot be empty.");

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                throw new DomainException("ComponentsJson must be a JSON array.");
            return json.Trim();
        }
        catch (JsonException)
        {
            throw new DomainException("ComponentsJson must be valid JSON.");
        }
    }
}
