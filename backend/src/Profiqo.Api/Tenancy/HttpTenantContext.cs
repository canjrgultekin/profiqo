using Profiqo.Application.Abstractions.Tenancy;
using Profiqo.Domain.Common.Ids;
using Profiqo.Api.Options;

namespace Profiqo.Api.Tenancy;

internal sealed class HttpTenantContext : ITenantContext
{
    private readonly IHttpContextAccessor _http;
    private readonly TenancyOptions _options;

    public HttpTenantContext(IHttpContextAccessor http, TenancyOptions options)
    {
        _http = http;
        _options = options;
    }

    public TenantId? CurrentTenantId
    {
        get
        {
            var ctx = _http.HttpContext;
            if (ctx is null) return null;

            if (!ctx.Request.Headers.TryGetValue(_options.TenantHeaderName, out var values))
                return null;

            var raw = values.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(raw)) return null;

            return Guid.TryParse(raw, out var g) ? new TenantId(g) : null;
        }
    }
}