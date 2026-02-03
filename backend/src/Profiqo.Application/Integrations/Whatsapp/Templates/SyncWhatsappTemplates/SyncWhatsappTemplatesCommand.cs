using Profiqo.Application.Common.Messaging;

namespace Profiqo.Application.Integrations.Whatsapp.Templates.SyncWhatsappTemplates;

public sealed record SyncWhatsappTemplatesCommand(Guid ConnectionId) : ICommand<int>;