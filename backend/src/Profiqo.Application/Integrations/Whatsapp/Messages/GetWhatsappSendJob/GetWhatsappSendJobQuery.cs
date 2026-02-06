using Profiqo.Application.Abstractions.Persistence.Whatsapp;
using Profiqo.Application.Common.Messaging;

namespace Profiqo.Application.Integrations.Whatsapp.Messages.GetWhatsappSendJob;

public sealed record GetWhatsappSendJobQuery(Guid JobId) : IQuery<WhatsappSendJobDto?>;