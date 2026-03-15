using Profiqo.Application.Abstractions.Integrations.Whatsapp;
using Profiqo.Application.Common.Messaging;

namespace Profiqo.Application.Integrations.Whatsapp.Commands.TestWhatsapp;

public sealed record TestWhatsappCommand(Guid ConnectionId) : ICommand<WhatsappPhoneNumberInfo>;