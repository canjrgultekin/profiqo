using Profiqo.Domain.Common;
using Profiqo.Domain.Common.Ids;

namespace Profiqo.Application.Abstractions.Tenancy;

public interface ITenantContext
{
    TenantId? CurrentTenantId { get; }
}