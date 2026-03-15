// Path: backend/src/Profiqo.Infrastructure/Persistence/QueryTypes/CustomerOrderAggRowDb.cs
namespace Profiqo.Infrastructure.Persistence.QueryTypes;

// NOTE: Property names intentionally snake_case to match FromSql result columns
internal sealed class CustomerOrderAggRowDb
{
    public Guid customer_id { get; set; }
    public short channel { get; set; }
    public int orders_count { get; set; }
    public decimal total_amount { get; set; }
    public string currency { get; set; } = "TRY";
}