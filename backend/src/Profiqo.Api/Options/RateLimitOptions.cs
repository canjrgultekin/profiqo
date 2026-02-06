namespace Profiqo.Api.Options;

public sealed class RateLimitOptions
{
    public int GlobalRps { get; init; } = 50;
}