// Profiqo.Infrastructure/Persistence/Converters/CurrencyCodeConverter.cs
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Profiqo.Domain.Common.Types;

namespace Profiqo.Infrastructure.Persistence.Converters;

public sealed class CurrencyCodeConverter : ValueConverter<CurrencyCode, string>
{
    public CurrencyCodeConverter() : base(
        c => c.Value,
        v => new CurrencyCode(v))
    { }
}