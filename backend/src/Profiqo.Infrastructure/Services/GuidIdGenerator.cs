using Profiqo.Application.Abstractions.Id;

namespace Profiqo.Infrastructure.Services;

internal sealed class GuidIdGenerator : IIdGenerator
{
    public Guid NewGuid() => Guid.NewGuid();
}