using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

using Profiqo.Application.Abstractions.Tenancy;
using Profiqo.Domain.Common.Ids;
using Profiqo.Domain.Tenants;

namespace Profiqo.Infrastructure.Persistence.Interceptors;

internal sealed class TenantEnforcementSaveChangesInterceptor : SaveChangesInterceptor
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

        // Bootstrap detection: Tenant entity being added means registration flow
        var isBootstrap = changeTracker.Entries()
            .Any(e => e.Entity is Tenant && e.State == EntityState.Added);

        foreach (var entry in changeTracker.Entries())
        {
            if (entry.State is EntityState.Detached or EntityState.Unchanged)
                continue;

            // Tenant entity'si kendi kendini yönetir - skip
            if (entry.Entity is Tenant)
                continue;

            var tenantProp = entry.Metadata.FindProperty("TenantId");
            if (tenantProp is null)
                continue;

            var currentValue = entry.Property("TenantId").CurrentValue;

            // Bootstrap senaryosu: Tenant context yok ama entity'nin TenantId'si var
            if (currentTenantId is null)
            {
                // Bootstrap flow'da entity kendi TenantId'sini set etmiş olmalı
                if (isBootstrap && HasValidTenantId(currentValue))
                    continue;

                throw new InvalidOperationException(
                    "Tenant context missing. Refusing to write tenant-scoped entity without tenant.");
            }

            var expected = currentTenantId.Value;

            // TenantId null ise, context'ten ata
            if (currentValue is null)
            {
                entry.Property("TenantId").CurrentValue = expected;
                continue;
            }

            // TenantId tipine göre karşılaştır
            if (currentValue is TenantId typed)
            {
                if (typed != expected)
                    throw new InvalidOperationException(
                        "Cross-tenant write detected. Refusing to persist entity with mismatching TenantId.");
                continue;
            }

            if (currentValue is Guid guid)
            {
                if (guid != expected.Value)
                    throw new InvalidOperationException(
                        "Cross-tenant write detected. Refusing to persist entity with mismatching TenantId.");
                continue;
            }

            throw new InvalidOperationException(
                $"Unsupported TenantId CLR type: {currentValue.GetType().FullName}");
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