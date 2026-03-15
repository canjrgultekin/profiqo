using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Profiqo.Domain.Customers;

namespace Profiqo.Infrastructure.Persistence.Converters;

internal sealed class IdentityHashConverter : ValueConverter<IdentityHash, string>
{
    public IdentityHashConverter()
        : base(
            v => v.Value,
            v => new IdentityHash(v))
    {
    }
}