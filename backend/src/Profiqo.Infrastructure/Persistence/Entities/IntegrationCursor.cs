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

    public IntegrationCursor(Guid id, TenantId tenantId, ProviderConnectionId providerConnectionId, string cursorKey, string cursorValue, DateTimeOffset updatedAtUtc)
    {
        Id = id;
        TenantId = tenantId;
        ProviderConnectionId = providerConnectionId;
        CursorKey = cursorKey;
        CursorValue = cursorValue;
        UpdatedAtUtc = updatedAtUtc;
    }

    public void Update(string cursorValue, DateTimeOffset nowUtc)
    {
        CursorValue = cursorValue;
        UpdatedAtUtc = nowUtc;
    }
}