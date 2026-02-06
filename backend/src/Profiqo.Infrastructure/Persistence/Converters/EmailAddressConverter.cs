using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Profiqo.Domain.Users;

namespace Profiqo.Infrastructure.Persistence.Converters;

internal sealed class EmailAddressConverter : ValueConverter<EmailAddress, string>
{
    public EmailAddressConverter()
        : base(
            v => v.Value,
            v => new EmailAddress(v))
    {
    }
}