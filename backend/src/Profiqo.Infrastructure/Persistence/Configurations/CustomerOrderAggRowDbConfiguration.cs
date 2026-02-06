// Path: backend/src/Profiqo.Infrastructure/Persistence/Configurations/CustomerOrderAggRowDbConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Profiqo.Infrastructure.Persistence.QueryTypes;

namespace Profiqo.Infrastructure.Persistence.Configurations;

internal sealed class CustomerOrderAggRowDbConfiguration : IEntityTypeConfiguration<CustomerOrderAggRowDb>
{
    public void Configure(EntityTypeBuilder<CustomerOrderAggRowDb> builder)
    {
        builder.HasNoKey();
        builder.ToView(null);
    }
}