namespace Profiqo.Api.Health;

public static class HealthChecks
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/health/live", () => Results.Ok(new { status = "ok" }));
        app.MapGet("/health/ready", () => Results.Ok(new { status = "ok" }));
    }
}