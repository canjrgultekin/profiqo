using Profiqo.Application.Common.Messaging;

namespace Profiqo.Application.Integrations.Ikas.Commands.SyncIkasCustomers;

public sealed record SyncIkasCustomersCommand(Guid ConnectionId, int? PageSize, int? MaxPages) : ICommand<int>;