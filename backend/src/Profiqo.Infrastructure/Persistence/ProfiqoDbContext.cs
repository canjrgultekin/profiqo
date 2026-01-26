using Microsoft.EntityFrameworkCore;

using Profiqo.Application.Abstractions.Tenancy;
using Profiqo.Domain.Automation;
using Profiqo.Domain.Common.Ids;
using Profiqo.Domain.Customers;
using Profiqo.Domain.Integrations;
using Profiqo.Domain.Messaging;
using Profiqo.Domain.Orders;
using Profiqo.Domain.Tenants;
using Profiqo.Domain.Users;
using Profiqo.Infrastructure.Persistence.Entities;

namespace Profiqo.Infrastructure.Persistence;

public sealed class ProfiqoDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;

    public TenantId? CurrentTenantId => _tenantContext.CurrentTenantId;

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();

    public DbSet<ProviderConnection> ProviderConnections => Set<ProviderConnection>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<MessageTemplate> MessageTemplates => Set<MessageTemplate>();
    public DbSet<AutomationRule> AutomationRules => Set<AutomationRule>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();
    public DbSet<IngestionEvent> IngestionEvents => Set<IngestionEvent>();
    public DbSet<IntegrationCursor> IntegrationCursors => Set<IntegrationCursor>();

    public ProfiqoDbContext(DbContextOptions<ProfiqoDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ProfiqoDbContext).Assembly);

        // Force owned type discovery for EF (prevents "CustomerConsent requires PK" issue)
        modelBuilder.Owned<CustomerConsent>();

        // Tenant scoping (fail-closed)
        modelBuilder.Entity<Customer>().HasQueryFilter(x => CurrentTenantId != null && x.TenantId == CurrentTenantId.Value);
        modelBuilder.Entity<Order>().HasQueryFilter(x => CurrentTenantId != null && x.TenantId == CurrentTenantId.Value);
        modelBuilder.Entity<ProviderConnection>().HasQueryFilter(x => CurrentTenantId != null && x.TenantId == CurrentTenantId.Value);
        modelBuilder.Entity<MessageTemplate>().HasQueryFilter(x => CurrentTenantId != null && x.TenantId == CurrentTenantId.Value);
        modelBuilder.Entity<AutomationRule>().HasQueryFilter(x => CurrentTenantId != null && x.TenantId == CurrentTenantId.Value);

        modelBuilder.Entity<OutboxMessage>().HasQueryFilter(x => CurrentTenantId != null && x.TenantId == CurrentTenantId.Value);
        modelBuilder.Entity<InboxMessage>().HasQueryFilter(x => CurrentTenantId != null && x.TenantId == CurrentTenantId.Value);
        modelBuilder.Entity<IngestionEvent>().HasQueryFilter(x => CurrentTenantId != null && x.TenantId == CurrentTenantId.Value);
        modelBuilder.Entity<IntegrationCursor>().HasQueryFilter(x => CurrentTenantId != null && x.TenantId == CurrentTenantId.Value);

        base.OnModelCreating(modelBuilder);
    }
}
