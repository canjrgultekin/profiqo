using Profiqo.Application.Common.Messaging;

namespace Profiqo.Application.Integrations.Whatsapp.Commands.UpsertWhatsappConnection;

public sealed record UpsertWhatsappConnectionCommand(
    string DisplayName,
    string WabaId,
    string PhoneNumberId,
    string AccessToken,
    bool IsTestMode
) : ICommand<Guid>;