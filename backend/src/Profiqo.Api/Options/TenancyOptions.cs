namespace Profiqo.Api.Options;

public sealed class TenancyOptions
{
    public string TenantHeaderName { get; init; } = "X-Tenant-Id";
}