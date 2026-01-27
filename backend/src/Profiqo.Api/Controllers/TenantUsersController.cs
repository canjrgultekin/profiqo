using MediatR;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Profiqo.Api.Security;
using Profiqo.Application.Tenants.Users.CreateTenantUser;
using Profiqo.Application.Tenants.Users.ListTenantUsers;
using Profiqo.Application.Tenants.Users.SetTenantUserStatus;

namespace Profiqo.Api.Controllers;

[ApiController]
[Route("api/tenant/users")]
[Authorize(Policy = AuthorizationPolicies.OwnerOnly)]
public sealed class TenantUsersController : ControllerBase
{
    private readonly ISender _sender;

    public TenantUsersController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var items = await _sender.Send(new ListTenantUsersQuery(), ct);
        return Ok(new { items });
    }

    public sealed record CreateUserRequest(string Email, string DisplayName, string Password, string Role);

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest req, CancellationToken ct)
    {
        var id = await _sender.Send(new CreateTenantUserCommand(req.Email, req.DisplayName, req.Password, req.Role), ct);
        return Ok(new { userId = id });
    }

    [HttpPost("{userId:guid}/disable")]
    public async Task<IActionResult> Disable([FromRoute] Guid userId, CancellationToken ct)
    {
        var ok = await _sender.Send(new SetTenantUserStatusCommand(userId, false), ct);
        if (!ok) return NotFound(new { message = "User not found." });
        return Ok(new { ok = true });
    }

    [HttpPost("{userId:guid}/activate")]
    public async Task<IActionResult> Activate([FromRoute] Guid userId, CancellationToken ct)
    {
        var ok = await _sender.Send(new SetTenantUserStatusCommand(userId, true), ct);
        if (!ok) return NotFound(new { message = "User not found." });
        return Ok(new { ok = true });
    }
}