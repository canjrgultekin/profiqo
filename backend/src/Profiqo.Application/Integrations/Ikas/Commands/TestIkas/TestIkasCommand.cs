using Profiqo.Application.Common.Messaging;

namespace Profiqo.Application.Integrations.Ikas.Commands.TestIkas;

public sealed record TestIkasCommand(Guid ConnectionId) : ICommand<string>;