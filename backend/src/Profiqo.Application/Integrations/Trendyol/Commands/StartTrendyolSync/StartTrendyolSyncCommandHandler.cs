using MediatR;

using Profiqo.Application.Abstractions.Persistence;
using Profiqo.Application.Abstractions.Persistence.Repositories;
using Profiqo.Application.Abstractions.Tenancy;
using Profiqo.Application.Common.Exceptions;
using Profiqo.Application.Integrations.Jobs;
using Profiqo.Domain.Common.Ids;
using Profiqo.Domain.Integrations;

namespace Profiqo.Application.Integrations.Trendyol.Commands.StartTrendyolSync;

internal sealed class StartTrendyolSyncCommandHandler : IRequestHandler<StartTrendyolSyncCommand, StartTrendyolSyncResult>
{
    private readonly ITenantContext _tenant;
    private readonly IProviderConnectionRepository _connections;
    private readonly IIntegrationJobRepository _jobs;

    public StartTrendyolSyncCommandHandler(
        ITenantContext tenant,
        IProviderConnectionRepository connections,
        IIntegrationJobRepository jobs)
    {
        _tenant = tenant;
        _connections = connections;
        _jobs = jobs;
    }

    public async Task<StartTrendyolSyncResult> Handle(StartTrendyolSyncCommand request, CancellationToken ct)
    {
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null) throw new UnauthorizedException("Tenant context missing.");

        var conn = await _connections.GetByIdAsync(new ProviderConnectionId(request.ConnectionId), ct);
        if (conn is null || conn.TenantId != tenantId.Value || conn.ProviderType != ProviderType.Trendyol)
            throw new NotFoundException("Trendyol connection not found.");

        var pageSize = request.PageSize is null or < 1 or > 200 ? 50 : request.PageSize.Value;
        var maxPages = request.MaxPages is null or < 1 or > 500 ? 20 : request.MaxPages.Value;

        var batchId = Guid.NewGuid();

        var jobId = await _jobs.CreateAsync(new IntegrationJobCreateRequest(
            BatchId: batchId,
            TenantId: tenantId.Value.Value,
            ConnectionId: request.ConnectionId,
            Kind: IntegrationJobKind.TrendyolSyncOrders,
            PageSize: pageSize,
            MaxPages: maxPages), ct);

        return new StartTrendyolSyncResult(batchId, new[] { new StartTrendyolSyncJob(jobId, nameof(IntegrationJobKind.TrendyolSyncOrders)) });
    }
}
