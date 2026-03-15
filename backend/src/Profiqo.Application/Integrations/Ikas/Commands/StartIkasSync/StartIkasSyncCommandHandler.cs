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
    // Cursor keys (IkasSyncProcessor ile aynı)
    private const string CustomerCursorKey = "ikas.customers.cursor.updatedAtMs";
    private const string OrderCursorKey = "ikas.orders.cursor.updatedAtMs";
    private const string AbandonedCursorKey = "ikas.abandoned.cursor.lastActivityDateMs";
    private const string ProductCursorKey = "ikas.products.cursor.updatedAtMs";

    private const int DefaultMaxPagesInitial = 200;
    private const int DefaultMaxPagesSubsequent = 20;

    private readonly ITenantContext _tenant;
    private readonly IProviderConnectionRepository _connections;
    private readonly IIntegrationJobRepository _jobs;
    private readonly IIntegrationCursorRepository _cursors;

    public StartIkasSyncCommandHandler(
        ITenantContext tenant,
        IProviderConnectionRepository connections,
        IIntegrationJobRepository jobs,
        IIntegrationCursorRepository cursors)
    {
        _tenant = tenant;
        _connections = connections;
        _jobs = jobs;
        _cursors = cursors;
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

        int maxPages;
        if (request.MaxPages is not null && request.MaxPages.Value is >= 1 and <= 500)
        {
            maxPages = request.MaxPages.Value;
        }
        else
        {
            var connId = new ProviderConnectionId(request.ConnectionId);

            var scopeRaw = (request.Scope ?? "both").Trim().ToLowerInvariant();
            var wantsCustomers = scopeRaw is "customers" or "both";
            var wantsOrders = scopeRaw is "orders" or "both";
            var wantsAbandoned = scopeRaw is "abandoned" or "both";
            var wantsProducts = scopeRaw is "products" or "both";

            var isInitial = false;

            if (wantsCustomers)
            {
                var c = await _cursors.GetAsync(tenantId.Value, connId, CustomerCursorKey, ct);
                if (!long.TryParse(c, out var ms) || ms <= 0) isInitial = true;
            }

            if (!isInitial && wantsOrders)
            {
                var c = await _cursors.GetAsync(tenantId.Value, connId, OrderCursorKey, ct);
                if (!long.TryParse(c, out var ms) || ms <= 0) isInitial = true;
            }

            if (!isInitial && wantsAbandoned)
            {
                var c = await _cursors.GetAsync(tenantId.Value, connId, AbandonedCursorKey, ct);
                if (!long.TryParse(c, out var ms) || ms <= 0) isInitial = true;
            }
            if (!isInitial && wantsProducts)
            {
                var c = await _cursors.GetAsync(tenantId.Value, connId, ProductCursorKey, ct);
                if (!long.TryParse(c, out var ms) || ms <= 0) isInitial = true;
            }
            maxPages = isInitial ? DefaultMaxPagesInitial : DefaultMaxPagesSubsequent;
        }

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
        if (scope is "products" or "both")
        {
            var jobId = await _jobs.CreateAsync(new IntegrationJobCreateRequest(
                BatchId: batchId,
                TenantId: tenantId.Value.Value,
                ConnectionId: request.ConnectionId,
                Kind: IntegrationJobKind.IkasSyncProducts,
                PageSize: pageSize,
                MaxPages: maxPages), ct);

            created.Add(new StartIkasSyncJob(jobId, nameof(IntegrationJobKind.IkasSyncProducts)));
        }
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


            var jobId4 = await _jobs.CreateAsync(new IntegrationJobCreateRequest(
                BatchId: batchId,
                TenantId: tenantId.Value.Value,
                ConnectionId: request.ConnectionId,
                Kind: IntegrationJobKind.IkasSyncProducts,
                PageSize: pageSize,
                MaxPages: maxPages), ct);

            created.Add(new StartIkasSyncJob(jobId4, nameof(IntegrationJobKind.IkasSyncProducts)));
        }

        return new StartIkasSyncResult(batchId, created);
    }
}
