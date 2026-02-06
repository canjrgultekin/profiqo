using Microsoft.EntityFrameworkCore;

using Profiqo.Application.Abstractions.Persistence.Repositories;
using Profiqo.Domain.Automation;
using Profiqo.Domain.Common.Ids;

namespace Profiqo.Infrastructure.Persistence.Repositories;

internal sealed class AutomationRuleRepository : IAutomationRuleRepository
{
    private readonly ProfiqoDbContext _db;

    public AutomationRuleRepository(ProfiqoDbContext db)
    {
        _db = db;
    }

    public Task<AutomationRule?> GetByIdAsync(AutomationRuleId id, CancellationToken cancellationToken)
        => _db.AutomationRules.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<AutomationRule>> ListActiveAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        return await _db.AutomationRules
            .Where(x => x.TenantId == tenantId && x.Status == AutomationRuleStatus.Active)
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(AutomationRule rule, CancellationToken cancellationToken)
    {
        await _db.AutomationRules.AddAsync(rule, cancellationToken);
    }
}