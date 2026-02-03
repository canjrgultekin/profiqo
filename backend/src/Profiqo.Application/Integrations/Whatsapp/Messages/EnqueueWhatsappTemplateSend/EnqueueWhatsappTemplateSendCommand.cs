using System.Text.Json;

using Profiqo.Application.Common.Messaging;

namespace Profiqo.Application.Integrations.Whatsapp.Messages.EnqueueWhatsappTemplateSend;

public sealed record EnqueueWhatsappTemplateSendCommand(
    Guid ConnectionId,
    string ToPhoneE164,
    string TemplateName,
    string LanguageCode,
    JsonElement? Components,
    bool? UseMarketingEndpoint
) : ICommand<Guid>;