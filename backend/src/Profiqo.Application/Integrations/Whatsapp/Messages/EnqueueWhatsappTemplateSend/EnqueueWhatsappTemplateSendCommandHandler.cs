using System.Text.Json;

using MediatR;

using Profiqo.Application.Abstractions.Crypto;
using Profiqo.Application.Abstractions.Integrations.Whatsapp;
using Profiqo.Application.Abstractions.Persistence.Repositories;
using Profiqo.Application.Abstractions.Persistence.Whatsapp;
using Profiqo.Application.Abstractions.Tenancy;
using Profiqo.Application.Common.Exceptions;
using Profiqo.Domain.Common.Ids;
using Profiqo.Domain.Integrations;

namespace Profiqo.Application.Integrations.Whatsapp.Messages.EnqueueWhatsappTemplateSend;

internal sealed class EnqueueWhatsappTemplateSendCommandHandler : IRequestHandler<EnqueueWhatsappTemplateSendCommand, Guid>
{
    private readonly ITenantContext _tenant;
    private readonly IProviderConnectionRepository _connections;
    private readonly ISecretProtector _secrets;
    private readonly IWhatsappSendJobRepository _jobs;

    public EnqueueWhatsappTemplateSendCommandHandler(
        ITenantContext tenant,
        IProviderConnectionRepository connections,
        ISecretProtector secrets,
        IWhatsappSendJobRepository jobs)
    {
        _tenant = tenant;
        _connections = connections;
        _secrets = secrets;
        _jobs = jobs;
    }

    public async Task<Guid> Handle(EnqueueWhatsappTemplateSendCommand request, CancellationToken ct)
    {
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null) throw new UnauthorizedException("Tenant context missing.");

        var conn = await _connections.GetByIdAsync(new ProviderConnectionId(request.ConnectionId), ct);
        if (conn is null || conn.ProviderType != ProviderType.Whatsapp)
            throw new NotFoundException("whatsapp_connection_not_found-WhatsApp connection not found.");

        var to = (request.ToPhoneE164 ?? "").Trim();
        if (string.IsNullOrWhiteSpace(to))
            throw new AppValidationException(new Dictionary<string, string[]> { ["toPhoneE164"] = new[] { "ToPhoneE164 required." } });

        var templateName = WhatsappTemplateNameNormalizer.Normalize(request.TemplateName);
        var lang = (request.LanguageCode ?? "tr").Trim().ToLowerInvariant();

        var secretJson = _secrets.Unprotect(conn.AccessToken);
        var secret = WhatsappConnectionSecret.FromJson(secretJson);

        var payload = BuildPayloadJson(
            phoneNumberId: secret.PhoneNumberId,
            to: to,
            templateName: templateName,
            language: lang,
            components: request.Components,
            useMarketing: request.UseMarketingEndpoint);

        var jobId = await _jobs.CreateAsync(
            new WhatsappSendJobCreateRequest(
                TenantId: tenantId.Value.Value,
                ConnectionId: conn.Id.Value,
                PayloadJson: payload,
                NextAttemptAtUtc: DateTimeOffset.UtcNow),
            ct);

        return jobId;
    }

    private static string BuildPayloadJson(
        string phoneNumberId,
        string to,
        string templateName,
        string language,
        JsonElement? components,
        bool? useMarketing)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            w.WriteStartObject();
            w.WriteString("phoneNumberId", phoneNumberId);
            w.WriteString("to", to);
            w.WriteString("templateName", templateName);
            w.WriteString("language", language);

            if (useMarketing.HasValue) w.WriteBoolean("useMarketingEndpoint", useMarketing.Value);

            if (components.HasValue)
            {
                w.WritePropertyName("components");
                components.Value.WriteTo(w);
            }

            w.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }
}
