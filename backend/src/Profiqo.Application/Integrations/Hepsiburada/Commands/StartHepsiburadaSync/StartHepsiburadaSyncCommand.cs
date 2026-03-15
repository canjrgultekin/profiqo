// Path: backend/src/Profiqo.Application/Integrations/Hepsiburada/Commands/StartHepsiburadaSync/StartHepsiburadaSyncCommand.cs
using Profiqo.Application.Common.Messaging;

namespace Profiqo.Application.Integrations.Hepsiburada.Commands.StartHepsiburadaSync;

public sealed record StartHepsiburadaSyncCommand(Guid ConnectionId, int? PageSize, int? MaxPages)
    : ICommand<StartHepsiburadaSyncResult>;

public sealed record StartHepsiburadaSyncResult(Guid BatchId, IReadOnlyList<StartHepsiburadaSyncJob> Jobs);
public sealed record StartHepsiburadaSyncJob(Guid JobId, string Kind);