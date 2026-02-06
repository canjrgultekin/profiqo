namespace Profiqo.Api.Options;

public sealed class ObservabilityOptions
{
    public string OtlpEndpoint { get; init; } = "http://localhost:4317";
}