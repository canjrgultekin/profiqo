using Profiqo.Domain.Common.Ids;

namespace Profiqo.Infrastructure.Persistence.Entities;

public sealed class IntegrationCursor
{
    public Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public ProviderConnectionId ProviderConnectionId { get; private set; }

    public string CursorKey { get; private set; } = string.Empty;
    public string CursorValue { get; private set; } = string.Empty;

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    private IntegrationCursor() { }

    public IntegrationCursor(Guid id, TenantId tenantId, ProviderConnectionId providerConnectionId, string cursorKey, string cursorValue)
    {
        if (id == Guid.Empty) throw new ArgumentException("Id cannot be empty.", nameof(id));
        if (string.IsNullOrWhiteSpace(cursorKey)) throw new ArgumentException("CursorKey is required.", nameof(cursorKey));

        Id = id;
        TenantId = tenantId;
        ProviderConnectionId = providerConnectionId;
        CursorKey = cursorKey;
        CursorValue = cursorValue;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void Update(string cursorValue)
    {
        CursorValue = cursorValue;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}