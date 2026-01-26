using Profiqo.Application.Common.Messaging;

namespace Profiqo.Application.Integrations.Ikas.Commands.StartIkasSync;

public enum IkasSyncScope : short
{
    Customers = 1,
    Orders = 2,
    Both = 3
}

public sealed record StartIkasSyncCommand(
    Guid ConnectionId,
    IkasSyncScope Scope,
    int? PageSize,
    int? MaxPages
) : ICommand<StartIkasSyncResult>;

public sealed record StartIkasSyncResult(Guid BatchId, IReadOnlyList<StartIkasSyncJob> Jobs);

public sealed record StartIkasSyncJob(Guid JobId, string Kind);