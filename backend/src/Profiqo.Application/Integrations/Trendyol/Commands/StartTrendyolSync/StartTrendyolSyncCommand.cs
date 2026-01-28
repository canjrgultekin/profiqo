using MediatR;

namespace Profiqo.Application.Integrations.Trendyol.Commands.StartTrendyolSync;

public sealed record StartTrendyolSyncCommand(Guid ConnectionId, int? PageSize, int? MaxPages) : IRequest<StartTrendyolSyncResult>;

public sealed record StartTrendyolSyncResult(Guid BatchId, IReadOnlyList<StartTrendyolSyncJob> Jobs);
public sealed record StartTrendyolSyncJob(Guid JobId, string Kind);