using MediatR;

namespace Profiqo.Application.Common.Messaging;

public interface IQuery<out TResponse> : IRequest<TResponse>
{
}