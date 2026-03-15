// Path: backend/src/Profiqo.Application/Integrations/Hepsiburada/Commands/TestHepsiburada/TestHepsiburadaCommand.cs
using MediatR;

namespace Profiqo.Application.Integrations.Hepsiburada.Commands.TestHepsiburada;

public sealed record TestHepsiburadaCommand(Guid ConnectionId) : IRequest<bool>;