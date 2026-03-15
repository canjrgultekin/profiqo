// Path: backend/src/Profiqo.Application/Integrations/Hepsiburada/Commands/ConnectHepsiburada/ConnectHepsiburadaCommandHandler.cs
using System.Text.Json;

using MediatR;

using Microsoft.Extensions.Options;

using Profiqo.Application.Abstractions.Crypto;
using Profiqo.Application.Abstractions.Integrations.Hepsiburada;
using Profiqo.Application.Abstractions.Persistence.Repositories;
using Profiqo.Application.Abstractions.Tenancy;
using Profiqo.Application.Common.Exceptions;
using Profiqo.Domain.Integrations;

namespace Profiqo.Application.Integrations.Hepsiburada.Commands.ConnectHepsiburada;

internal sealed class ConnectHepsiburadaCommandHandler : IRequestHandler<ConnectHepsiburadaCommand, Guid>
{
    private readonly ITenantContext _tenant;
    private readonly IProviderConnectionRepository _connections;
    private readonly ISecretProtector _secrets;
    private readonly IHepsiburadaClient _client;
    private readonly HepsiburadaOptions _opts;

    public ConnectHepsiburadaCommandHandler(
        ITenantContext tenant,
        IProviderConnectionRepository connections,
        ISecretProtector secrets,
        IHepsiburadaClient client,
        IOptions<HepsiburadaOptions> opts)
    {
        _tenant = tenant;
        _connections = connections;
        _secrets = secrets;
        _client = client;
        _opts = opts.Value;
    }

    public async Task<Guid> Handle(ConnectHepsiburadaCommand request, CancellationToken ct)
    {
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null)
            throw new UnauthorizedException("Tenant context missing.");

        var merchantId = (request.MerchantId ?? "").Trim();
        var username = (request.Username ?? "").Trim();
        var password = (request.Password ?? "").Trim();
        var displayName = string.IsNullOrWhiteSpace(request.DisplayName) ? "Hepsiburada" : request.DisplayName.Trim();

        if (string.IsNullOrWhiteSpace(merchantId))
            throw new ArgumentException("MerchantId required.", nameof(request.MerchantId));

        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Username required.", nameof(request.Username));

        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Password required.", nameof(request.Password));

        // Validate connectivity: son 1 gün, 1 kayıt
        var endDate = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm");
        var beginDate = DateTimeOffset.UtcNow.AddDays(-1).ToString("yyyy-MM-dd HH:mm");

        using var testDoc = await _client.GetPaidOrdersAsync(
            username: username,
            password: password,
            merchantId: merchantId,
            offset: 0,
            limit: 1,
            beginDate: beginDate,
            endDate: endDate,
            ct: ct);

        // Store encrypted JSON { username, password }
        var credsJson = JsonSerializer.Serialize(new HepsiburadaCreds(username, password));
        var enc = _secrets.Protect(credsJson);

        var existing = await _connections.GetByProviderAsync(tenantId.Value, ProviderType.Hepsiburada, ct);

        if (existing is null)
        {
            var created = ProviderConnection.Create(
                tenantId: tenantId.Value,
                providerType: ProviderType.Hepsiburada,
                displayName: displayName,
                externalAccountId: merchantId,
                accessToken: enc,
                refreshToken: null,
                accessTokenExpiresAtUtc: null,
                nowUtc: DateTimeOffset.UtcNow);

            await _connections.AddAsync(created, ct);
            return created.Id.Value;
        }

        existing.UpdateProfile(displayName, merchantId, DateTimeOffset.UtcNow);
        existing.RotateTokens(enc, null, null, DateTimeOffset.UtcNow);

        return existing.Id.Value;
    }

    internal sealed record HepsiburadaCreds(string Username, string Password);
}