// Path: backend/src/Profiqo.Application/Integrations/Trendyol/Commands/StartTrendyolSync/StartTrendyolSyncCommandHandler.cs
using MediatR;

using Profiqo.Application.Abstractions.Integrations.Trendyol;
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
    private const string CursorKey = "trendyol.orders.cursor.lastOrderDateMs";
    private const int DefaultMaxPagesInitial = 200;

    private readonly ITenantContext _tenant;
    private readonly IProviderConnectionRepository _connections;
    private readonly IIntegrationJobRepository _jobs;
    private readonly IIntegrationCursorRepository _cursors;
    private readonly TrendyolOptions _opts;

    public StartTrendyolSyncCommandHandler(
        ITenantContext tenant,
        IProviderConnectionRepository connections,
        IIntegrationJobRepository jobs,
        IIntegrationCursorRepository cursors,
        Microsoft.Extensions.Options.IOptions<TrendyolOptions> opts)
    {
        _tenant = tenant;
        _connections = connections;
        _jobs = jobs;
        _cursors = cursors;
        _opts = opts.Value;
    }

    public async Task<StartTrendyolSyncResult> Handle(StartTrendyolSyncCommand request, CancellationToken ct)
    {
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null)
            throw new UnauthorizedException("Tenant context missing.");

        var conn = await _connections.GetByIdAsync(new ProviderConnectionId(request.ConnectionId), ct);
        if (conn is null || conn.TenantId != tenantId.Value || conn.ProviderType != ProviderType.Trendyol)
            throw new NotFoundException($"Trendyol connection not found for tenant. connectionId={request.ConnectionId}");

        var pageSize = request.PageSize is null or < 1 ? _opts.DefaultPageSize : Math.Min(request.PageSize.Value, _opts.PageSizeMax);

        int maxPages;
        if (request.MaxPages is not null && request.MaxPages.Value is >= 1 and <= 500)
        {
            maxPages = request.MaxPages.Value;
        }
        else
        {
            var connId = new ProviderConnectionId(request.ConnectionId);
            var raw = await _cursors.GetAsync(tenantId.Value, connId, CursorKey, ct);
            var isInitial = !long.TryParse(raw, out var ms) || ms <= 0;
            maxPages = isInitial ? DefaultMaxPagesInitial : _opts.DefaultMaxPages;
        }

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
