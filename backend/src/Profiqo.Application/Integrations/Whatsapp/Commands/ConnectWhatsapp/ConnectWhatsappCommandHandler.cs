using System.Text.Json;

using MediatR;

using Microsoft.EntityFrameworkCore;

using Profiqo.Application.Abstractions.Crypto;
using Profiqo.Application.Abstractions.Integrations.Whatsapp;
using Profiqo.Application.Abstractions.Persistence.Repositories;
using Profiqo.Application.Abstractions.Tenancy;
using Profiqo.Application.Common.Exceptions;
using Profiqo.Domain.Integrations;

namespace Profiqo.Application.Integrations.Whatsapp.Commands.ConnectWhatsapp;

internal sealed class ConnectWhatsappCommandHandler : IRequestHandler<ConnectWhatsappCommand, Guid>
{
    private readonly ITenantContext _tenant;
    private readonly IProviderConnectionRepository _connections;
    private readonly ISecretProtector _secrets;
    private readonly IWhatsappGraphApiClient _wa;

    public ConnectWhatsappCommandHandler(
        ITenantContext tenant,
        IProviderConnectionRepository connections,
        ISecretProtector secrets,
        IWhatsappGraphApiClient wa)
    {
        _tenant = tenant;
        _connections = connections;
        _secrets = secrets;
        _wa = wa;
    }

    public async Task<Guid> Handle(ConnectWhatsappCommand request, CancellationToken ct)
    {
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null) throw new UnauthorizedException("Tenant context missing.");

        var displayName = (request.DisplayName ?? string.Empty).Trim();
        var wabaId = (request.WabaId ?? string.Empty).Trim();
        var phoneNumberId = (request.PhoneNumberId ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(displayName))
            throw new AppValidationException(new Dictionary<string, string[]> { ["displayName"] = new[] { "DisplayName required." } });

        if (string.IsNullOrWhiteSpace(wabaId))
            throw new AppValidationException(new Dictionary<string, string[]> { ["wabaId"] = new[] { "WabaId required." } });

        if (string.IsNullOrWhiteSpace(phoneNumberId))
            throw new AppValidationException(new Dictionary<string, string[]> { ["phoneNumberId"] = new[] { "PhoneNumberId required." } });

        var now = DateTimeOffset.UtcNow;

        for (var attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                // Token/permission doğrulaması
                try
                {
                    _ = await _wa.GetPhoneNumberInfoAsync(phoneNumberId, ct);
                }
                catch (Exception ex)
                {
                    throw new ExternalServiceAuthException("whatsapp", $"Cloud API access failed. Check SystemUserAccessToken permissions. {ex.Message}");
                }

                var secretJson = JsonSerializer.Serialize(new WhatsappConnectionSecret(wabaId, phoneNumberId));
                var enc = _secrets.Protect(secretJson);

                var existing = await _connections.GetByProviderAsync(tenantId.Value, ProviderType.Whatsapp, ct);

                if (existing is null)
                {
                    var created = ProviderConnection.Create(
                        tenantId: tenantId.Value,
                        providerType: ProviderType.Whatsapp,
                        displayName: displayName,
                        externalAccountId: wabaId,
                        accessToken: enc,
                        refreshToken: null,
                        accessTokenExpiresAtUtc: null,
                        nowUtc: now);

                    await _connections.AddAsync(created, ct);
                    return created.Id.Value;
                }

                existing.UpdateProfile(displayName, wabaId, now);
                existing.RotateTokens(enc, null, null, now);

                return existing.Id.Value;
            }
            catch (DbUpdateConcurrencyException) when (attempt == 1)
            {
                await _connections.ClearTrackingAsync(ct);
            }
        }

        throw new ConflictException("concurrency_conflict = Connection was updated concurrently. Please retry.");
    }
}
