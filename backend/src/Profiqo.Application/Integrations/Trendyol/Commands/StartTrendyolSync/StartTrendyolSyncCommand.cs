// Path: backend/src/Profiqo.Application/Integrations/Trendyol/Commands/StartTrendyolSync/StartTrendyolSyncCommand.cs
using Profiqo.Application.Common.Messaging;

namespace Profiqo.Application.Integrations.Trendyol.Commands.StartTrendyolSync;

public sealed record StartTrendyolSyncCommand(Guid ConnectionId, int? PageSize, int? MaxPages)
    : ICommand<StartTrendyolSyncResult>;

public sealed record StartTrendyolSyncResult(Guid BatchId, IReadOnlyList<StartTrendyolSyncJob> Jobs);
public sealed record StartTrendyolSyncJob(Guid JobId, string Kind);