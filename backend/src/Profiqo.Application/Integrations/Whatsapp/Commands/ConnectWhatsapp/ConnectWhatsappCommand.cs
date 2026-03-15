using Profiqo.Application.Common.Messaging;

namespace Profiqo.Application.Integrations.Whatsapp.Commands.ConnectWhatsapp;

public sealed record ConnectWhatsappCommand(
    string DisplayName,
    string WabaId,
    string PhoneNumberId
) : ICommand<Guid>;