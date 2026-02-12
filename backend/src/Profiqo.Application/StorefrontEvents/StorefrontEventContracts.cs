using System.Text.Json.Serialization;

namespace Profiqo.Application.StorefrontEvents;

// ── Request: POST /api/v1/events/storefront ──────────────────────

public sealed record StorefrontEventBatchRequest
{
    public string ApiKey { get; init; } = string.Empty;
    public string DeviceId { get; init; } = string.Empty;
    public string? SessionId { get; init; }
    public DateTimeOffset? SentAt { get; init; }
    public StorefrontCustomerContext? Customer { get; init; }
    public StorefrontTenantContext? Tenant { get; init; }
    public List<StorefrontEventItem> Events { get; init; } = [];
}

public sealed record StorefrontCustomerContext
{
    public string? Id { get; init; }
    public string? Email { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? Phone { get; init; }
    public bool IsGuest { get; init; } = true;
}

public sealed record StorefrontTenantContext
{
    public string? TenantId { get; init; }
    public string? TenantName { get; init; }
    public string? StoreDomain { get; init; }
}

public sealed record StorefrontEventItem
{
    public string? EventId { get; init; }
    public string Type { get; init; } = string.Empty;
    public DateTimeOffset? OccurredAt { get; init; }

    [JsonExtensionData]
    public Dictionary<string, object?>? ExtensionData { get; init; }

    /// <summary>Event-specific data (product, cart, order, etc.)</summary>
    public Dictionary<string, object?>? Data { get; init; }

    public StorefrontPageContext? Page { get; init; }
    public StorefrontCustomerContext? Customer { get; init; }
}

public sealed record StorefrontPageContext
{
    public string? Url { get; init; }
    public string? Path { get; init; }
    public string? Referrer { get; init; }
    public string? Title { get; init; }
    public string? UserAgent { get; init; }
    public string? Language { get; init; }
    public int? ScreenWidth { get; init; }
    public int? ScreenHeight { get; init; }
}

// ── Response ─────────────────────────────────────────────────────

public sealed record StorefrontEventBatchResponse
{
    public int Accepted { get; init; }
    public int Rejected { get; init; }
    public List<string>? Errors { get; init; }
}

// ── Response: GET /api/v1/events/storefront/config ───────────────

public sealed record StorefrontConfigResponse
{
    public string TenantId { get; init; } = string.Empty;
    public string? TenantName { get; init; }
    public string? StoreDomain { get; init; }
    public List<string> EnabledEvents { get; init; } = [];
}
