// Path: backend/src/Profiqo.Api/Controllers/TenantUsersController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using Profiqo.Api.Security;
using Profiqo.Application.Abstractions.Security;
using Profiqo.Application.Abstractions.Tenancy;
using Profiqo.Domain.Common.Ids;
using Profiqo.Domain.Users;
using Profiqo.Infrastructure.Persistence;

using DomainUser = Profiqo.Domain.Users.User;

namespace Profiqo.Api.Controllers;

[ApiController]
[Route("api/tenant/users")]
[Authorize(Policy = AuthorizationPolicies.OwnerOnly)]
public sealed class TenantUsersController : ControllerBase
{
    private readonly ProfiqoDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IPasswordHasher _hasher;

    public TenantUsersController(ProfiqoDbContext db, ITenantContext tenant, IPasswordHasher hasher)
    {
        _db = db;
        _tenant = tenant;
        _hasher = hasher;
    }

    public sealed record CreateUserRequest(string Email, string DisplayName, string Password, string Role);

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null) return BadRequest(new { message = "Tenant context missing." });

        var entities = await _db.Users.AsNoTracking()
            .Where(x => x.TenantId == tenantId.Value)
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync(ct);

        var users = entities.Select(x => new
        {
            userId = x.Id.Value,
            email = x.Email.Value,
            displayName = x.DisplayName,
            status = x.Status.ToString(),
            roles = x.Roles.Select(r => r.ToString()).ToArray(),
            createdAtUtc = x.CreatedAtUtc
        }).ToList();

        return Ok(new { items = users });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest req, CancellationToken ct)
    {
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null) return BadRequest(new { message = "Tenant context missing." });

        var email = (req.Email ?? "").Trim().ToLowerInvariant();
        var displayName = (req.DisplayName ?? "").Trim();
        var password = req.Password ?? "";
        var roleRaw = (req.Role ?? "").Trim();

        if (string.IsNullOrWhiteSpace(email)) return BadRequest(new { message = "Email required." });
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8) return BadRequest(new { message = "Password min 8 chars." });

        if (!Enum.TryParse<UserRole>(roleRaw, ignoreCase: true, out var role))
            return BadRequest(new { message = "Invalid role." });

        if (role == UserRole.Owner)
            return BadRequest(new { message = "Owner role cannot be assigned from UI." });

        // ✅ Tenant-scoped uniqueness check via raw SQL (avoids owned/value-object translation issues)
        var exists = await _db.Users
            .FromSqlInterpolated($@"SELECT * FROM public.users WHERE tenant_id = {tenantId.Value.Value} AND email = {email} LIMIT 1")
            .AsNoTracking()
            .AnyAsync(ct);

        if (exists) return Conflict(new { message = "Email already exists in this tenant." });

        var user = DomainUser.Create(
            UserId.New(),
            tenantId.Value,
            new EmailAddress(email),
            _hasher.Hash(password),
            string.IsNullOrWhiteSpace(displayName) ? email : displayName,
            new[] { role });

        await _db.Users.AddAsync(user, ct);
        await _db.SaveChangesAsync(ct);

        return Ok(new { userId = user.Id.Value });
    }

    [HttpPost("{userId:guid}/disable")]
    public async Task<IActionResult> Disable([FromRoute] Guid userId, CancellationToken ct)
    {
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null) return BadRequest(new { message = "Tenant context missing." });

        var u = await _db.Users.FirstOrDefaultAsync(x => x.TenantId == tenantId.Value && x.Id.Value == userId, ct);
        if (u is null) return NotFound(new { message = "User not found." });

        if (u.Roles.Contains(UserRole.Owner))
            return BadRequest(new { message = "Owner cannot be disabled." });

        u.Disable();
        await _db.SaveChangesAsync(ct);

        return Ok(new { ok = true });
    }

    [HttpPost("{userId:guid}/activate")]
    public async Task<IActionResult> Activate([FromRoute] Guid userId, CancellationToken ct)
    {
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null) return BadRequest(new { message = "Tenant context missing." });

        var u = await _db.Users.FirstOrDefaultAsync(x => x.TenantId == tenantId.Value && x.Id.Value == userId, ct);
        if (u is null) return NotFound(new { message = "User not found." });

        u.Activate();
        await _db.SaveChangesAsync(ct);

        return Ok(new { ok = true });
    }
}
