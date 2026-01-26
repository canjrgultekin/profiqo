using MediatR;

using Microsoft.Extensions.Logging;

namespace Profiqo.Application.Common.Behaviors;

internal sealed class RequestLoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<RequestLoggingBehavior<TRequest, TResponse>> _logger;

    public RequestLoggingBehavior(ILogger<RequestLoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;

        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["request_name"] = requestName
        });

        _logger.LogInformation("Handling request {RequestName}", requestName);

        try
        {
            var response = await next();
            _logger.LogInformation("Handled request {RequestName}", requestName);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception while handling request {RequestName}", requestName);
            throw;
        }
    }
}