using MediatR;

using Microsoft.Extensions.Options;

using Profiqo.Application.Abstractions.Integrations.Shopify;
using Profiqo.Application.Abstractions.Persistence;
using Profiqo.Application.Abstractions.Persistence.Repositories;
using Profiqo.Application.Abstractions.Tenancy;
using Profiqo.Application.Common.Exceptions;
using Profiqo.Application.Integrations.Jobs;
using Profiqo.Domain.Common.Ids;
using Profiqo.Domain.Integrations;

namespace Profiqo.Application.Integrations.Shopify.Commands.StartShopifySync;

internal sealed class StartShopifySyncCommandHandler : IRequestHandler<StartShopifySyncCommand, StartShopifySyncResult>
{
    private readonly ITenantContext _tenant;
    private readonly IProviderConnectionRepository _connections;
    private readonly IIntegrationJobRepository _jobs;
    private readonly ShopifyOptions _opts;

    public StartShopifySyncCommandHandler(ITenantContext tenant, IProviderConnectionRepository connections,
        IIntegrationJobRepository jobs, IOptions<ShopifyOptions> opts)
    { _tenant = tenant; _connections = connections; _jobs = jobs; _opts = opts.Value; }

    public async Task<StartShopifySyncResult> Handle(StartShopifySyncCommand request, CancellationToken ct)
    {
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null) throw new UnauthorizedException("Tenant context missing.");

        var conn = await _connections.GetByIdAsync(new ProviderConnectionId(request.ConnectionId), ct);
        if (conn is null || conn.TenantId != tenantId.Value || conn.ProviderType != ProviderType.Shopify)
            throw new NotFoundException($"Shopify connection not found. connectionId={request.ConnectionId}");

        var pageSize = request.PageSize is null or < 1 ? _opts.DefaultPageSize : Math.Min(request.PageSize.Value, 250);
        var maxPages = request.MaxPages is not null && request.MaxPages.Value is >= 1 and <= 500 ? request.MaxPages.Value : _opts.DefaultMaxPages;

        var batchId = Guid.NewGuid();
        var jobs = new List<StartShopifySyncJob>();
        var scope = (request.Scope ?? "all").Trim().ToLowerInvariant();

        if (scope is "all" or "customers" or "both")
        {
            var jid = await _jobs.CreateAsync(new IntegrationJobCreateRequest(batchId, tenantId.Value.Value, request.ConnectionId, IntegrationJobKind.ShopifySyncCustomers, pageSize, maxPages), ct);
            jobs.Add(new StartShopifySyncJob(jid, nameof(IntegrationJobKind.ShopifySyncCustomers)));
        }
        if (scope is "all" or "orders" or "both")
        {
            var jid = await _jobs.CreateAsync(new IntegrationJobCreateRequest(batchId, tenantId.Value.Value, request.ConnectionId, IntegrationJobKind.ShopifySyncOrders, pageSize, maxPages), ct);
            jobs.Add(new StartShopifySyncJob(jid, nameof(IntegrationJobKind.ShopifySyncOrders)));
        }
        if (scope is "all" or "products")
        {
            var jid = await _jobs.CreateAsync(new IntegrationJobCreateRequest(batchId, tenantId.Value.Value, request.ConnectionId, IntegrationJobKind.ShopifySyncProducts, pageSize, maxPages), ct);
            jobs.Add(new StartShopifySyncJob(jid, nameof(IntegrationJobKind.ShopifySyncProducts)));
        }

        return new StartShopifySyncResult(batchId, jobs);
    }
}