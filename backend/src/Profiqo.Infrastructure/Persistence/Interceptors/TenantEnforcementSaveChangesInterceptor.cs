// Path: backend/src/Profiqo.Infrastructure/Persistence/Interceptors/TenantEnforcementSaveChangesInterceptor.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

using Profiqo.Application.Abstractions.Tenancy;
using Profiqo.Domain.Common.Ids;
using Profiqo.Domain.Tenants;

namespace Profiqo.Infrastructure.Persistence.Interceptors;

public sealed class TenantEnforcementSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly ITenantContext _tenantContext;

    public TenantEnforcementSaveChangesInterceptor(ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        var ctx = eventData.Context;
        if (ctx is null) return base.SavingChangesAsync(eventData, result, cancellationToken);

        EnforceTenant(ctx.ChangeTracker);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        var ctx = eventData.Context;
        if (ctx is null) return base.SavingChanges(eventData, result);

        EnforceTenant(ctx.ChangeTracker);
        return base.SavingChanges(eventData, result);
    }

    private void EnforceTenant(ChangeTracker changeTracker)
    {
        var currentTenantId = _tenantContext.CurrentTenantId;

        foreach (var entry in changeTracker.Entries())
        {
            if (entry.State is EntityState.Detached or EntityState.Unchanged)
                continue;

            // Tenant entity kendi kendini yönetir
            if (entry.Entity is Tenant)
                continue;

            var tenantProp = entry.Metadata.FindProperty("TenantId");
            if (tenantProp is null)
                continue;

            var currentValue = entry.Property("TenantId").CurrentValue;

            // ── Senaryo 1: Tenant context mevcut (JWT authenticated flow) ──
            if (currentTenantId is not null)
            {
                var expected = currentTenantId.Value;

                // TenantId null ise context'ten ata
                if (currentValue is null)
                {
                    entry.Property("TenantId").CurrentValue = expected;
                    continue;
                }

                // Cross-tenant write kontrolü
                if (currentValue is TenantId typed)
                {
                    if (typed != expected)
                        throw new InvalidOperationException(
                            $"Cross-tenant write detected. Entity TenantId={typed.Value}, Context TenantId={expected.Value}.");
                    continue;
                }

                if (currentValue is Guid guid)
                {
                    if (guid != expected.Value)
                        throw new InvalidOperationException(
                            $"Cross-tenant write detected. Entity TenantId={guid}, Context TenantId={expected.Value}.");
                    continue;
                }

                throw new InvalidOperationException(
                    $"Unsupported TenantId CLR type: {currentValue.GetType().FullName}");
            }

            // ── Senaryo 2: Tenant context yok (Pixel, background job, registration) ──
            // Entity'nin TenantId'si code tarafından zaten set edilmiş mi kontrol et.
            // Set edilmişse izin ver; edilmemişse reddet.
            if (HasValidTenantId(currentValue))
                continue;

            throw new InvalidOperationException(
                "Tenant context missing and entity has no valid TenantId. " +
                "Refusing to write tenant-scoped entity without tenant identification.");
        }
    }

    private static bool HasValidTenantId(object? value)
    {
        return value switch
        {
            TenantId id => id != default,
            Guid guid => guid != Guid.Empty,
            _ => false
        };
    }
}