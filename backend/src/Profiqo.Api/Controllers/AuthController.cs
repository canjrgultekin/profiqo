using MediatR;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Profiqo.Api.Security;
using Profiqo.Application.Abstractions.Persistence.Repositories;
using Profiqo.Application.Auth.Commands.Login;
using Profiqo.Application.Auth.Commands.RegisterTenant;
using Profiqo.Domain.Common.Ids;

namespace Profiqo.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly ISender _sender;

    public AuthController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterTenantCommand cmd, CancellationToken ct)
    {
        var result = await _sender.Send(cmd, ct);
        return Ok(result);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginCommand cmd, CancellationToken ct)
    {
        var result = await _sender.Send(cmd, ct);
        return Ok(result);
    }
    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me(
        [FromServices] CurrentUserContext currentUser,
        [FromServices] IUserRepository users,
        CancellationToken ct)
    {
        if (currentUser.UserId is null)
            return Unauthorized(new { message = "Not authenticated." });

        var userId = new UserId(currentUser.UserId.Value);
        var user = await users.GetByIdAsync(userId, ct);

        if (user is null)
            return NotFound(new { message = "User not found." });

        return Ok(new
        {
            userId = user.Id.Value,
            tenantId = user.TenantId.Value,
            email = user.Email.Value,
            displayName = user.DisplayName,
            status = (short)user.Status,
            roles = user.Roles.Select(r => (short)r).ToArray()
        });
    }
}