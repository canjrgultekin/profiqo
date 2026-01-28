using MediatR;

namespace Profiqo.Application.Integrations.Trendyol.Commands.ConnectTrendyol;

public sealed record ConnectTrendyolCommand(
    string DisplayName,
    string SupplierId,
    string ApiKey,
    string ApiSecret
) : IRequest<Guid>;