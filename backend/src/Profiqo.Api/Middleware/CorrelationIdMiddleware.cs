namespace Profiqo.Api.Middleware;

internal sealed class CorrelationIdMiddleware : IMiddleware
{
    public const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var cid = context.Request.Headers[HeaderName].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(cid))
            cid = Guid.NewGuid().ToString("N");

        context.Items[HeaderName] = cid;
        context.Response.Headers[HeaderName] = cid;

        using (Serilog.Context.LogContext.PushProperty("correlation_id", cid))
        {
            await next(context);
        }
    }
}