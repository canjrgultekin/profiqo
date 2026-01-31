// Path: backend/src/Profiqo.Api/Controllers/CustomerDedupeController.cs
using MediatR;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Profiqo.Api.Security;
using Profiqo.Application.Customers.Dedupe;
using Profiqo.Application.Abstractions.Tenancy;

namespace Profiqo.Api.Controllers;

[ApiController]
[Route("api/customers/dedupe")]
[Authorize(Policy = AuthorizationPolicies.ReportAccess)]
public sealed class CustomerDedupeController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ITenantContext _tenant;
    private readonly ICustomerMergeSuggestionRepository _suggestions;

    public CustomerDedupeController(ISender sender, ITenantContext tenant, ICustomerMergeSuggestionRepository suggestions)
    {
        _sender = sender;
        _tenant = tenant;
        _suggestions = suggestions;
    }

    public sealed record AnalyzeRequest(double? Threshold);

    [HttpPost("analyze")]
    public async Task<IActionResult> Analyze([FromBody] AnalyzeRequest req, CancellationToken ct)
    {
        var result = await _sender.Send(new AnalyzeCustomerDuplicatesQuery(req.Threshold), ct);
        return Ok(result);
    }

    [HttpGet("suggestions")]
    public async Task<IActionResult> ListSuggestions([FromQuery] int take = 50, CancellationToken ct = default)
    {
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null) return BadRequest(new { message = "Tenant context missing." });

        var items = await _suggestions.ListLatestAsync(tenantId.Value, take, ct);
        return Ok(new { items });
    }

    [HttpGet("suggestions/{groupKey}")]
    public async Task<IActionResult> GetSuggestion([FromRoute] string groupKey, CancellationToken ct = default)
    {
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null) return BadRequest(new { message = "Tenant context missing." });

        var x = await _suggestions.GetByGroupKeyAsync(tenantId.Value, groupKey, ct);
        if (x is null) return NotFound(new { message = "Suggestion not found." });

        return Ok(x);
    }
}
