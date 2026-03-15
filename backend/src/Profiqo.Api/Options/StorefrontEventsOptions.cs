namespace Profiqo.Api.Options;

public sealed class StorefrontEventsOptions
{
    public string ScriptBaseUrl { get; set; } = "https://profiqocdn.z1.web.core.windows.net/ikas/v2";
    public string ScriptFileName { get; set; } = "profiqo-ikas-events.min.js";

    /// <summary>
    /// CORS'a izin verilen origin pattern'leri.
    /// Wildcard: "*.myikas.com" → host.EndsWith(".myikas.com")
    /// </summary>
    public string[] AllowedOrigins { get; set; } =
    [
        "*.myikas.com",
        "*.ikas.com",
        "localhost"
    ];
}
