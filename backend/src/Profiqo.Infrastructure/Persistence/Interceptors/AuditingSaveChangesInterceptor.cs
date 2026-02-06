using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Profiqo.Infrastructure.Persistence.Interceptors;

internal sealed class AuditingSaveChangesInterceptor : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        var ctx = eventData.Context;
        if (ctx is null) return base.SavingChangesAsync(eventData, result, cancellationToken);

        ApplyAudit(ctx.ChangeTracker);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        var ctx = eventData.Context;
        if (ctx is null) return base.SavingChanges(eventData, result);

        ApplyAudit(ctx.ChangeTracker);
        return base.SavingChanges(eventData, result);
    }

    private static void ApplyAudit(ChangeTracker changeTracker)
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var entry in changeTracker.Entries())
        {
            if (entry.State is EntityState.Detached or EntityState.Unchanged)
                continue;

            SetIfExists(entry, "UpdatedAtUtc", now);

            if (entry.State == EntityState.Added)
                SetIfExists(entry, "CreatedAtUtc", now);
        }
    }

    private static void SetIfExists(EntityEntry entry, string propertyName, DateTimeOffset value)
    {
        var prop = entry.Metadata.FindProperty(propertyName);
        if (prop is null) return;

        entry.Property(propertyName).CurrentValue = value;
    }
}