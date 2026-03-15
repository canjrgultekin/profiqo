// Path: backend/src/Profiqo.Application/Integrations/Hepsiburada/Commands/ConnectHepsiburada/ConnectHepsiburadaCommand.cs
using Profiqo.Application.Common.Messaging;

namespace Profiqo.Application.Integrations.Hepsiburada.Commands.ConnectHepsiburada;

public sealed record ConnectHepsiburadaCommand(
    string DisplayName,
    string MerchantId,
    string Username,
    string Password
) : ICommand<Guid>;