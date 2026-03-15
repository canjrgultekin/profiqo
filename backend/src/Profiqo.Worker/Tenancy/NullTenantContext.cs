using Profiqo.Application.Abstractions.Tenancy;
using Profiqo.Domain.Common.Ids;

namespace Profiqo.Worker.Tenancy;

internal sealed class NullTenantContext : ITenantContext
{
    public TenantId? CurrentTenantId => null;
}