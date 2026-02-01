using Profiqo.Application.Common.Messaging;

namespace Profiqo.Application.Integrations.Ikas.Commands.ConnectIkas;

public sealed record ConnectIkasCommand(
    string StoreLabel,
    string StoreName,
    string ClientId,
    string ClientSecret
) : ICommand<Guid>;