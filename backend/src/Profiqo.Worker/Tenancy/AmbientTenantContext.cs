using System.Threading;

using Profiqo.Application.Abstractions.Tenancy;
using Profiqo.Domain.Common.Ids;

namespace Profiqo.Worker.Tenancy;

public interface ITenantContextSetter
{
    void Set(TenantId tenantId);
    void Clear();
}

internal sealed class AmbientTenantContext : ITenantContext, ITenantContextSetter
{
    private static readonly AsyncLocal<Guid?> Current = new();

    public TenantId? CurrentTenantId
        => Current.Value.HasValue ? new TenantId(Current.Value.Value) : null;

    public void Set(TenantId tenantId) => Current.Value = tenantId.Value;
    public void Clear() => Current.Value = null;
}