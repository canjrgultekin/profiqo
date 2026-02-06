using System.Text.Json;

using MediatR;

using Microsoft.Extensions.Options;

using Profiqo.Application.Abstractions.Crypto;
using Profiqo.Application.Abstractions.Integrations.Whatsapp;
using Profiqo.Application.Abstractions.Persistence.Repositories;
using Profiqo.Application.Abstractions.Tenancy;
using Profiqo.Application.Common.Exceptions;
using Profiqo.Domain.Integrations;

namespace Profiqo.Application.Integrations.Whatsapp.Commands.UpsertWhatsappConnection;

internal sealed class UpsertWhatsappConnectionCommandHandler : IRequestHandler<UpsertWhatsappConnectionCommand, Guid>
{
    private readonly ITenantContext _tenant;
    private readonly IProviderConnectionRepository _connections;
    private readonly ISecretProtector _secrets;
    private readonly WhatsappIntegrationOptions _opt;
    private readonly IWhatsappCloudValidator _validator;

    public UpsertWhatsappConnectionCommandHandler(
        ITenantContext tenant,
        IProviderConnectionRepository connections,
        ISecretProtector secrets,
        IOptions<WhatsappIntegrationOptions> opt,
        IWhatsappCloudValidator validator)
    {
        _tenant = tenant;
        _connections = connections;
        _secrets = secrets;
        _opt = opt.Value;
        _validator = validator;
    }

    public async Task<Guid> Handle(UpsertWhatsappConnectionCommand request, CancellationToken ct)
    {
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null)
            throw new UnauthorizedException("Tenant context missing.");

        var displayName = (request.DisplayName ?? "").Trim();
        var wabaId = (request.WabaId ?? "").Trim();
        var phoneId = (request.PhoneNumberId ?? "").Trim();
        var token = (request.AccessToken ?? "").Trim();

        if (string.IsNullOrWhiteSpace(displayName)) throw new AppValidationException(new Dictionary<string, string[]> { ["displayName"] = new[] { "Required." } });
        if (string.IsNullOrWhiteSpace(wabaId)) throw new AppValidationException(new Dictionary<string, string[]> { ["wabaId"] = new[] { "Required." } });
        if (string.IsNullOrWhiteSpace(phoneId)) throw new AppValidationException(new Dictionary<string, string[]> { ["phoneNumberId"] = new[] { "Required." } });
        if (string.IsNullOrWhiteSpace(token)) throw new AppValidationException(new Dictionary<string, string[]> { ["accessToken"] = new[] { "Required." } });

        var effectiveTestMode = _opt.ForceTestMode || request.IsTestMode;

        // Secret JSON -> encrypt -> provider_connections.access_token_ciphertext
        var secretJson = JsonSerializer.Serialize(new WhatsappCredentialSecret(token, phoneId, wabaId));
        var enc = _secrets.Protect(secretJson);

        var now = DateTimeOffset.UtcNow;

        var existing = await _connections.GetByProviderAsync(tenantId.Value, ProviderType.Whatsapp, ct);

        ProviderConnection conn;
        if (existing is null)
        {
            conn = ProviderConnection.Create(
                tenantId: tenantId.Value,
                providerType: ProviderType.Whatsapp,
                displayName: displayName,
                externalAccountId: wabaId,
                accessToken: enc,
                refreshToken: null,
                accessTokenExpiresAtUtc: null,
                nowUtc: now,
                isTestMode: effectiveTestMode);

            await _connections.AddAsync(conn, ct);
        }
        else
        {
            existing.UpdateProfile(displayName, wabaId, now);
            existing.RotateTokens(enc, null, null, now);
            existing.SetTestMode(effectiveTestMode, now);
            conn = existing;
        }

        // TestMode değilse validation deneriz, başarısızsa InvalidCredentials’e çekeriz ama kaydı tutarız.
        if (!effectiveTestMode)
        {
            var (ok, _, _, rawError) = await _validator.ValidateAsync(token, phoneId, ct);
            if (!ok)
                conn.MarkInvalidCredentials(now);
        }

        return conn.Id.Value;
    }
}
