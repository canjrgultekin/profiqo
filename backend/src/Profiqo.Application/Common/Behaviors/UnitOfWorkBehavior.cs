// Path: backend/src/Profiqo.Application/Common/Behaviors/UnitOfWorkBehavior.cs
using MediatR;

using Profiqo.Application.Abstractions.Persistence;
using Profiqo.Application.Common.Messaging;

namespace Profiqo.Application.Common.Behaviors;

internal sealed class UnitOfWorkBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IUnitOfWork _uow;

    public UnitOfWorkBehavior(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var response = await next();

        if (IsCommand(request))
            await _uow.SaveChangesAsync(cancellationToken);

        return response;
    }

    private static bool IsCommand(object request)
    {
        var type = request.GetType();

        // Generic ICommand<T>
        if (type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommand<>)))
            return true;

        // Optional non-generic marker (if exists in your codebase)
        return type.GetInterfaces().Any(i => i == typeof(ICommand<>));
    }
}