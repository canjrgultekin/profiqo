using Profiqo.Application.Common.Messaging;
namespace Profiqo.Application.Integrations.Shopify.Commands.StartShopifySync;

public sealed record StartShopifySyncCommand(Guid ConnectionId, string? Scope, int? PageSize, int? MaxPages)
    : ICommand<StartShopifySyncResult>;
public sealed record StartShopifySyncResult(Guid BatchId, IReadOnlyList<StartShopifySyncJob> Jobs);
public sealed record StartShopifySyncJob(Guid JobId, string Kind);