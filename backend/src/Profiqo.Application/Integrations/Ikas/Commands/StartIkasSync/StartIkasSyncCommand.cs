// Path: backend/src/Profiqo.Application/Integrations/Ikas/Commands/StartIkasSync/StartIkasSyncCommand.cs
using MediatR;

namespace Profiqo.Application.Integrations.Ikas.Commands.StartIkasSync;

// IMPORTANT: scope is carried as raw string to avoid enum-mapping mismatches.
public sealed record StartIkasSyncCommand(
    Guid ConnectionId,
    string Scope,
    int? PageSize,
    int? MaxPages
) : IRequest<StartIkasSyncResult>;

public sealed record StartIkasSyncResult(Guid BatchId, IReadOnlyList<StartIkasSyncJob> Jobs);

public sealed record StartIkasSyncJob(Guid JobId, string Kind);