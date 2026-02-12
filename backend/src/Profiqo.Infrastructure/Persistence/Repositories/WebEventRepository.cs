using System.Data;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using NpgsqlTypes;

using Profiqo.Application.StorefrontEvents;

namespace Profiqo.Infrastructure.Persistence.Repositories;

/// <summary>
/// WebEvent batch insert — yüksek hacimli event ingestion için raw SQL kullanır.
/// EF AddRange ile SaveChanges yapmak yerine direkt INSERT (change tracker yükü yok).
/// </summary>
public sealed class WebEventRepository : IWebEventRepository
{
    private readonly ProfiqoDbContext _db;

    public WebEventRepository(ProfiqoDbContext db) => _db = db;

    public async Task InsertBatchAsync(IReadOnlyList<WebEventInsert> events, CancellationToken ct)
    {
        if (events.Count == 0) return;

        const string sql = """
            INSERT INTO web_events
                (id, tenant_id, event_type, device_id_hash, session_id, customer_id,
                 client_ip, page_url, page_path, page_referrer, page_title, user_agent,
                 event_data, occurred_at_utc, created_at_utc)
            VALUES
                (@p0, @p1, @p2, @p3, @p4, @p5,
                 @p6, @p7, @p8, @p9, @p10, @p11,
                 @p12::jsonb, @p13, @p14)
            ON CONFLICT DO NOTHING
            """;

        foreach (var e in events)
        {
            var parameters = new NpgsqlParameter[]
            {
                new("p0", NpgsqlDbType.Uuid) { Value = e.Id },
                new("p1", NpgsqlDbType.Uuid) { Value = e.TenantId },
                new("p2", NpgsqlDbType.Varchar) { Value = e.EventType },
                new("p3", NpgsqlDbType.Varchar) { Value = e.DeviceIdHash },
                new("p4", NpgsqlDbType.Varchar) { Value = (object?)e.SessionId ?? DBNull.Value },
                new("p5", NpgsqlDbType.Uuid) { Value = (object?)e.CustomerId ?? DBNull.Value },
                new("p6", NpgsqlDbType.Varchar) { Value = (object?)e.ClientIp ?? DBNull.Value },
                new("p7", NpgsqlDbType.Varchar) { Value = (object?)e.PageUrl ?? DBNull.Value },
                new("p8", NpgsqlDbType.Varchar) { Value = (object?)e.PagePath ?? DBNull.Value },
                new("p9", NpgsqlDbType.Varchar) { Value = (object?)e.PageReferrer ?? DBNull.Value },
                new("p10", NpgsqlDbType.Varchar) { Value = (object?)e.PageTitle ?? DBNull.Value },
                new("p11", NpgsqlDbType.Varchar) { Value = (object?)e.UserAgent ?? DBNull.Value },
                new("p12", NpgsqlDbType.Text) { Value = e.EventDataJson },
                new("p13", NpgsqlDbType.TimestampTz) { Value = e.OccurredAtUtc },
                new("p14", NpgsqlDbType.TimestampTz) { Value = e.CreatedAtUtc },
            };

            await _db.Database.ExecuteSqlRawAsync(sql, parameters, ct);
        }
    }
}