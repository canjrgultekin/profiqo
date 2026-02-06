using System.Linq.Expressions;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Profiqo.Infrastructure.Persistence.Converters;

internal sealed class StronglyTypedIdConverter<TId> : ValueConverter<TId, Guid>
    where TId : struct
{
    public StronglyTypedIdConverter()
        : base(CreateToGuidExpression(), CreateFromGuidExpression())
    {
    }

    private static Expression<Func<TId, Guid>> CreateToGuidExpression()
    {
        var param = Expression.Parameter(typeof(TId), "id");
        var valueProp = Expression.Property(param, "Value"); // must exist
        var body = Expression.Convert(valueProp, typeof(Guid));
        return Expression.Lambda<Func<TId, Guid>>(body, param);
    }

    private static Expression<Func<Guid, TId>> CreateFromGuidExpression()
    {
        var param = Expression.Parameter(typeof(Guid), "value");
        var ctor = typeof(TId).GetConstructor(new[] { typeof(Guid) })
                   ?? throw new InvalidOperationException($"{typeof(TId).Name} must have a public constructor(Guid).");

        var body = Expression.New(ctor, param);
        return Expression.Lambda<Func<Guid, TId>>(body, param);
    }
}