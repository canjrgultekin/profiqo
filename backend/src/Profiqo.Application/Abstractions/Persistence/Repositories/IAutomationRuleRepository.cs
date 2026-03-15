using Profiqo.Domain.Common;
using Profiqo.Domain.Automation;
using Profiqo.Domain.Common.Ids;

namespace Profiqo.Application.Abstractions.Persistence.Repositories;

public interface IAutomationRuleRepository
{
    Task<AutomationRule?> GetByIdAsync(AutomationRuleId id, CancellationToken cancellationToken);

    Task<IReadOnlyList<AutomationRule>> ListActiveAsync(TenantId tenantId, CancellationToken cancellationToken);

    Task AddAsync(AutomationRule rule, CancellationToken cancellationToken);
}