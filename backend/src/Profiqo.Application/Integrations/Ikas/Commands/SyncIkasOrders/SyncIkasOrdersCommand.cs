using Profiqo.Application.Common.Messaging;

namespace Profiqo.Application.Integrations.Ikas.Commands.SyncIkasOrders;

public sealed record SyncIkasOrdersCommand(Guid ConnectionId, int? PageSize, int? MaxPages) : ICommand<int>;