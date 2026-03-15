// Path: backend/src/Profiqo.Application/Integrations/Hepsiburada/Commands/StartHepsiburadaSync/StartHepsiburadaSyncCommandHandler.cs
using MediatR;

using Profiqo.Application.Abstractions.Integrations.Hepsiburada;
using Profiqo.Application.Abstractions.Persistence;
using Profiqo.Application.Abstractions.Persistence.Repositories;
using Profiqo.Application.Abstractions.Tenancy;
using Profiqo.Application.Common.Exceptions;
using Profiqo.Application.Integrations.Jobs;
using Profiqo.Domain.Common.Ids;
using Profiqo.Domain.Integrations;

namespace Profiqo.Application.Integrations.Hepsiburada.Commands.StartHepsiburadaSync;

internal sealed class StartHepsiburadaSyncCommandHandler : IRequestHandler<StartHepsiburadaSyncCommand, StartHepsiburadaSyncResult>
{
    private readonly ITenantContext _tenant;
    private readonly IProviderConnectionRepository _connections;
    private readonly IIntegrationJobRepository _jobs;
    private readonly HepsiburadaOptions _opts;

    public StartHepsiburadaSyncCommandHandler(
        ITenantContext tenant,
        IProviderConnectionRepository connections,
        IIntegrationJobRepository jobs,
        Microsoft.Extensions.Options.IOptions<HepsiburadaOptions> opts)
    {
        _tenant = tenant;
        _connections = connections;
        _jobs = jobs;
        _opts = opts.Value;
    }

    public async Task<StartHepsiburadaSyncResult> Handle(StartHepsiburadaSyncCommand request, CancellationToken ct)
    {
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null)
            throw new UnauthorizedException("Tenant context missing.");

        var conn = await _connections.GetByIdAsync(new ProviderConnectionId(request.ConnectionId), ct);
        if (conn is null || conn.TenantId != tenantId.Value || conn.ProviderType != ProviderType.Hepsiburada)
            throw new NotFoundException($"Hepsiburada connection not found for tenant. connectionId={request.ConnectionId}");

        var pageSize = request.PageSize is null or < 1
            ? _opts.DefaultLimit
            : Math.Min(request.PageSize.Value, _opts.LimitMax);

        var maxPages = request.MaxPages is not null && request.MaxPages.Value is >= 1 and <= 500
            ? request.MaxPages.Value
            : _opts.DefaultMaxPages;

        var batchId = Guid.NewGuid();

        var jobId = await _jobs.CreateAsync(new IntegrationJobCreateRequest(
            BatchId: batchId,
            TenantId: tenantId.Value.Value,
            ConnectionId: request.ConnectionId,
            Kind: IntegrationJobKind.HepsiburadaSyncOrders,
            PageSize: pageSize,
            MaxPages: maxPages), ct);

        return new StartHepsiburadaSyncResult(batchId, new[] { new StartHepsiburadaSyncJob(jobId, nameof(IntegrationJobKind.HepsiburadaSyncOrders)) });
    }
}