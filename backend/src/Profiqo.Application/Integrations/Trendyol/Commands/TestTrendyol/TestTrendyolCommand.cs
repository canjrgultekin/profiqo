// Path: backend/src/Profiqo.Application/Integrations/Trendyol/Commands/TestTrendyol/TestTrendyolCommand.cs
using MediatR;

namespace Profiqo.Application.Integrations.Trendyol.Commands.TestTrendyol;

public sealed record TestTrendyolCommand(Guid ConnectionId) : IRequest<bool>;