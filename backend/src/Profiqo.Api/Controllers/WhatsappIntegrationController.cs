using MediatR;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using System.Text.Json;

using Profiqo.Api.Security;
using Profiqo.Application.Abstractions.Crypto;
using Profiqo.Application.Abstractions.Persistence.Repositories;
using Profiqo.Application.Abstractions.Tenancy;
using Profiqo.Application.Integrations.Whatsapp;
using Profiqo.Application.Integrations.Whatsapp.Commands.UpsertWhatsappConnection;
using Profiqo.Application.Integrations.Whatsapp.Queries.TestWhatsappConnection;
using Profiqo.Domain.Integrations;

namespace Profiqo.Api.Controllers;

[ApiController]
[Route("api/integrations/whatsapp")]
[Authorize(Policy = AuthorizationPolicies.IntegrationAccess)]
public sealed class WhatsappIntegrationController : ControllerBase
{
    private readonly ITenantContext _tenant;
    private readonly IProviderConnectionRepository _connections;
    private readonly ISecretProtector _secrets;
    private readonly ISender _sender;

    public WhatsappIntegrationController(
        ITenantContext tenant,
        IProviderConnectionRepository connections,
        ISecretProtector secrets,
        ISender sender)
    {
        _tenant = tenant;
        _connections = connections;
        _secrets = secrets;
        _sender = sender;
    }

    [HttpGet("connection")]
    public async Task<IActionResult> GetConnection(CancellationToken ct)
    {
        var tid = _tenant.CurrentTenantId;
        if (tid is null) return BadRequest(new { message = "Tenant context missing." });

        var conn = await _connections.GetByProviderAsync(tid.Value, ProviderType.Whatsapp, ct);
        if (conn is null) return Ok(new { hasConnection = false });

        // phoneNumberId token içinde, UI’a token dönmüyoruz.
        string? phoneNumberId = null;
        try
        {
            var secretJson = _secrets.Unprotect(conn.AccessToken);
            var secret = WhatsappCredentialSecret.FromJson(secretJson);
            phoneNumberId = secret.PhoneNumberId;
        }
        catch { }

        return Ok(new
        {
            hasConnection = true,
            connectionId = conn.Id.Value,
            displayName = conn.DisplayName,
            wabaId = conn.ExternalAccountId,
            phoneNumberId,
            status = conn.Status.ToString(),
            isTestMode = conn.IsTestMode
        });
    }

    public sealed record ConnectRequest(
        string DisplayName,
        string WabaId,
        string PhoneNumberId,
        string AccessToken,
        bool IsTestMode);

    [HttpPost("connect")]
    public async Task<IActionResult> Connect([FromBody] ConnectRequest req, CancellationToken ct)
    {
        var id = await _sender.Send(new UpsertWhatsappConnectionCommand(
            req.DisplayName, req.WabaId, req.PhoneNumberId, req.AccessToken, req.IsTestMode), ct);

        return Ok(new { connectionId = id });
    }

    public sealed record TestRequest(Guid ConnectionId);

    [HttpPost("test")]
    public async Task<IActionResult> Test([FromBody] TestRequest req, CancellationToken ct)
    {
        var result = await _sender.Send(new TestWhatsappConnectionQuery(req.ConnectionId), ct);
        return Ok(result);
    }
}
