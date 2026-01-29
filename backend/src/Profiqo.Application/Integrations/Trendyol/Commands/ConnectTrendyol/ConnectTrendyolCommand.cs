// Path: backend/src/Profiqo.Application/Integrations/Trendyol/Commands/ConnectTrendyol/ConnectTrendyolCommand.cs
using MediatR;

namespace Profiqo.Application.Integrations.Trendyol.Commands.ConnectTrendyol;

public sealed record ConnectTrendyolCommand(
    string DisplayName,
    string SellerId,
    string ApiKey,
    string ApiSecret,
    string? UserAgent
) : IRequest<Guid>;