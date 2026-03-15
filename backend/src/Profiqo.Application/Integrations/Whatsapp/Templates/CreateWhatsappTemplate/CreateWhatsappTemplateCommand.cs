using System.Text.Json;

using Profiqo.Application.Common.Messaging;

namespace Profiqo.Application.Integrations.Whatsapp.Templates.CreateWhatsappTemplate;

public sealed record CreateWhatsappTemplateCommand(
    Guid ConnectionId,
    string Name,
    string Language,
    string Category,
    JsonElement Components
) : ICommand<Guid>;