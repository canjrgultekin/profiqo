using MediatR;
namespace Profiqo.Application.Integrations.Shopify.Commands.TestShopify;

public sealed record TestShopifyCommand(Guid ConnectionId) : IRequest<bool>;