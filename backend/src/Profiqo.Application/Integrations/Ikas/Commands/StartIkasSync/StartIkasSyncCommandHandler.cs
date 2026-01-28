// Path: backend/src/Profiqo.Application/Integrations/Ikas/Commands/StartIkasSync/StartIkasSyncCommandHandler.cs
using MediatR;

using Profiqo.Application.Abstractions.Persistence;
using Profiqo.Application.Abstractions.Persistence.Repositories;
using Profiqo.Application.Abstractions.Tenancy;
using Profiqo.Application.Common.Exceptions;
using Profiqo.Application.Integrations.Jobs;
using Profiqo.Domain.Common.Ids;
using Profiqo.Domain.Integrations;

namespace Profiqo.Application.Integrations.Ikas.Commands.StartIkasSync;

internal sealed class StartIkasSyncCommandHandler : IRequestHandler<StartIkasSyncCommand, StartIkasSyncResult>
{
    private readonly ITenantContext _tenant;
    private readonly IProviderConnectionRepository _connections;
    private readonly IIntegrationJobRepository _jobs;

    public StartIkasSyncCommandHandler(
        ITenantContext tenant,
        IProviderConnectionRepository connections,
        IIntegrationJobRepository jobs)
    {
        _tenant = tenant;
        _connections = connections;
        _jobs = jobs;
    }

    public async Task<StartIkasSyncResult> Handle(StartIkasSyncCommand request, CancellationToken ct)
    {
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null)
            throw new UnauthorizedException("Tenant context missing.");

        var conn = await _connections.GetByIdAsync(new ProviderConnectionId(request.ConnectionId), ct);
        if (conn is null || conn.TenantId != tenantId.Value || conn.ProviderType != ProviderType.Ikas)
            throw new NotFoundException("Ikas connection not found.");

        var pageSize = request.PageSize is null or < 1 or > 200 ? 50 : request.PageSize.Value;
        var maxPages = request.MaxPages is null or < 1 or > 500 ? 20 : request.MaxPages.Value;

        // Normalize scope string
        var scope = (request.Scope ?? "both").Trim().ToLowerInvariant();

        var batchId = Guid.NewGuid();
        var created = new List<StartIkasSyncJob>();

        if (scope is "customers" or "both")
        {
            var jobId = await _jobs.CreateAsync(new IntegrationJobCreateRequest(
                BatchId: batchId,
                TenantId: tenantId.Value.Value,
                ConnectionId: request.ConnectionId,
                Kind: IntegrationJobKind.IkasSyncCustomers,
                PageSize: pageSize,
                MaxPages: maxPages), ct);

            created.Add(new StartIkasSyncJob(jobId, nameof(IntegrationJobKind.IkasSyncCustomers)));
        }

        if (scope is "orders" or "both")
        {
            var jobId = await _jobs.CreateAsync(new IntegrationJobCreateRequest(
                BatchId: batchId,
                TenantId: tenantId.Value.Value,
                ConnectionId: request.ConnectionId,
                Kind: IntegrationJobKind.IkasSyncOrders,
                PageSize: pageSize,
                MaxPages: maxPages), ct);

            created.Add(new StartIkasSyncJob(jobId, nameof(IntegrationJobKind.IkasSyncOrders)));
        }

        if (scope is "abandoned" or "both")
        {
            var jobId = await _jobs.CreateAsync(new IntegrationJobCreateRequest(
                BatchId: batchId,
                TenantId: tenantId.Value.Value,
                ConnectionId: request.ConnectionId,
                Kind: IntegrationJobKind.IkasSyncAbandonedCheckouts,
                PageSize: pageSize,
                MaxPages: maxPages), ct);

            created.Add(new StartIkasSyncJob(jobId, nameof(IntegrationJobKind.IkasSyncAbandonedCheckouts)));
        }

        // Safety: unknown scope => default both
        if (created.Count == 0)
        {
            var jobId1 = await _jobs.CreateAsync(new IntegrationJobCreateRequest(
                BatchId: batchId,
                TenantId: tenantId.Value.Value,
                ConnectionId: request.ConnectionId,
                Kind: IntegrationJobKind.IkasSyncCustomers,
                PageSize: pageSize,
                MaxPages: maxPages), ct);

            created.Add(new StartIkasSyncJob(jobId1, nameof(IntegrationJobKind.IkasSyncCustomers)));

            var jobId2 = await _jobs.CreateAsync(new IntegrationJobCreateRequest(
                BatchId: batchId,
                TenantId: tenantId.Value.Value,
                ConnectionId: request.ConnectionId,
                Kind: IntegrationJobKind.IkasSyncOrders,
                PageSize: pageSize,
                MaxPages: maxPages), ct);

            created.Add(new StartIkasSyncJob(jobId2, nameof(IntegrationJobKind.IkasSyncOrders)));

            var jobId3 = await _jobs.CreateAsync(new IntegrationJobCreateRequest(
                BatchId: batchId,
                TenantId: tenantId.Value.Value,
                ConnectionId: request.ConnectionId,
                Kind: IntegrationJobKind.IkasSyncAbandonedCheckouts,
                PageSize: pageSize,
                MaxPages: maxPages), ct);

            created.Add(new StartIkasSyncJob(jobId3, nameof(IntegrationJobKind.IkasSyncAbandonedCheckouts)));
        }

        return new StartIkasSyncResult(batchId, created);
    }
}
