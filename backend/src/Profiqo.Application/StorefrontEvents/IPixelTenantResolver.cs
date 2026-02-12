using Profiqo.Domain.Common.Ids;

namespace Profiqo.Application.StorefrontEvents;

/// <summary>
/// publicApiKey ile tenant çözümleme — Pixel integration kaydından.
/// provider_connections tablosunda ProviderType.Pixel kaydı olmalı.
/// external_account_id alanı publicApiKey olarak kullanılır.
/// </summary>
public interface IPixelTenantResolver
{
    /// <summary>publicApiKey ile tenant'ı çöz. Geçersiz key ise null döner.</summary>
    Task<ResolvedPixelTenant?> ResolveAsync(string publicApiKey, CancellationToken ct);
}

public sealed record ResolvedPixelTenant
{
    public required TenantId TenantId { get; init; }
    public required string TenantName { get; init; }
    public string? StoreDomain { get; init; }
    public List<string> EnabledEvents { get; init; } = [];
}
