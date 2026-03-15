using Profiqo.Application.Abstractions.Persistence.Whatsapp;
using Profiqo.Application.Common.Messaging;

namespace Profiqo.Application.Integrations.Whatsapp.Templates.ListWhatsappTemplates;

public sealed record ListWhatsappTemplatesQuery(Guid ConnectionId) : IQuery<IReadOnlyList<WhatsappTemplateDto>>;