using Microsoft.EntityFrameworkCore;

using Npgsql;

using Profiqo.Application.Abstractions.Persistence.Whatsapp;
using Profiqo.Infrastructure.Persistence;

namespace Profiqo.Infrastructure.Persistence.Repositories;

internal sealed class WhatsappTemplateRepository : IWhatsappTemplateRepository
{
    private readonly ProfiqoDbContext _db;

    public WhatsappTemplateRepository(ProfiqoDbContext db)
    {
        _db = db;
    }

    public async Task<WhatsappTemplateDto?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        const string sql = @"
SELECT id, tenant_id, connection_id, name, language, category, status,
       components_json::text, meta_template_id, rejection_reason,
       created_at_utc, updated_at_utc
FROM whatsapp_templates
WHERE id = @id
LIMIT 1;";

        await _db.Database.OpenConnectionAsync(ct);
        try
        {
            await using var cmd = _db.Database.GetDbConnection().CreateCommand();
            cmd.CommandText = sql;
            cmd.Parameters.Add(new NpgsqlParameter("id", id));

            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct)) return null;

            return Map(r);
        }
        finally
        {
            await _db.Database.CloseConnectionAsync();
        }
    }

    public async Task<WhatsappTemplateDto?> GetByNameAsync(Guid tenantId, Guid connectionId, string name, CancellationToken ct)
    {
        const string sql = @"
SELECT id, tenant_id, connection_id, name, language, category, status,
       components_json::text, meta_template_id, rejection_reason,
       created_at_utc, updated_at_utc
FROM whatsapp_templates
WHERE tenant_id = @tenant_id
  AND connection_id = @connection_id
  AND name = @name
LIMIT 1;";

        await _db.Database.OpenConnectionAsync(ct);
        try
        {
            await using var cmd = _db.Database.GetDbConnection().CreateCommand();
            cmd.CommandText = sql;
            cmd.Parameters.Add(new NpgsqlParameter("tenant_id", tenantId));
            cmd.Parameters.Add(new NpgsqlParameter("connection_id", connectionId));
            cmd.Parameters.Add(new NpgsqlParameter("name", (name ?? "").Trim().ToLowerInvariant()));

            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct)) return null;

            return Map(r);
        }
        finally
        {
            await _db.Database.CloseConnectionAsync();
        }
    }

    public async Task<IReadOnlyList<WhatsappTemplateDto>> ListAsync(Guid tenantId, Guid connectionId, CancellationToken ct)
    {
        const string sql = @"
SELECT id, tenant_id, connection_id, name, language, category, status,
       components_json::text, meta_template_id, rejection_reason,
       created_at_utc, updated_at_utc
FROM whatsapp_templates
WHERE tenant_id = @tenant_id
  AND connection_id = @connection_id
ORDER BY updated_at_utc DESC;";

        var list = new List<WhatsappTemplateDto>();

        await _db.Database.OpenConnectionAsync(ct);
        try
        {
            await using var cmd = _db.Database.GetDbConnection().CreateCommand();
            cmd.CommandText = sql;
            cmd.Parameters.Add(new NpgsqlParameter("tenant_id", tenantId));
            cmd.Parameters.Add(new NpgsqlParameter("connection_id", connectionId));

            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                list.Add(Map(r));

            return list;
        }
        finally
        {
            await _db.Database.CloseConnectionAsync();
        }
    }

    public async Task<Guid> UpsertAsync(WhatsappTemplateUpsertRequest req, CancellationToken ct)
    {
        const string sql = @"
INSERT INTO whatsapp_templates(
    id, tenant_id, connection_id, name, language, category, status,
    components_json, meta_template_id, rejection_reason,
    created_at_utc, updated_at_utc
)
VALUES (
    gen_random_uuid(), @tenant_id, @connection_id, @name, @language, @category, @status,
    CAST(@components_json AS jsonb), @meta_template_id, @rejection_reason,
    now(), now()
)
ON CONFLICT (tenant_id, connection_id, name)
DO UPDATE SET
  language = EXCLUDED.language,
  category = EXCLUDED.category,
  status = EXCLUDED.status,
  components_json = EXCLUDED.components_json,
  meta_template_id = COALESCE(EXCLUDED.meta_template_id, whatsapp_templates.meta_template_id),
  rejection_reason = EXCLUDED.rejection_reason,
  updated_at_utc = now()
RETURNING id;";

        await _db.Database.OpenConnectionAsync(ct);
        try
        {
            await using var cmd = _db.Database.GetDbConnection().CreateCommand();
            cmd.CommandText = sql;

            cmd.Parameters.Add(new NpgsqlParameter("tenant_id", req.TenantId));
            cmd.Parameters.Add(new NpgsqlParameter("connection_id", req.ConnectionId));
            cmd.Parameters.Add(new NpgsqlParameter("name", (req.Name ?? "").Trim().ToLowerInvariant()));
            cmd.Parameters.Add(new NpgsqlParameter("language", (req.Language ?? "tr").Trim().ToLowerInvariant()));
            cmd.Parameters.Add(new NpgsqlParameter("category", (req.Category ?? "MARKETING").Trim().ToUpperInvariant()));
            cmd.Parameters.Add(new NpgsqlParameter("status", (req.Status ?? "DRAFT").Trim().ToUpperInvariant()));
            cmd.Parameters.Add(new NpgsqlParameter("components_json", req.ComponentsJson ?? "[]"));
            cmd.Parameters.Add(new NpgsqlParameter("meta_template_id", (object?)req.MetaTemplateId ?? DBNull.Value));
            cmd.Parameters.Add(new NpgsqlParameter("rejection_reason", (object?)req.RejectionReason ?? DBNull.Value));

            var result = await cmd.ExecuteScalarAsync(ct);
            return result is Guid g ? g : Guid.Empty;
        }
        finally
        {
            await _db.Database.CloseConnectionAsync();
        }
    }

    public async Task UpdateMetaStatusAsync(Guid id, string status, string? rejectionReason, string? metaTemplateId, CancellationToken ct)
    {
        const string sql = @"
UPDATE whatsapp_templates
SET status = @status,
    rejection_reason = @rejection_reason,
    meta_template_id = COALESCE(@meta_template_id, meta_template_id),
    updated_at_utc = now()
WHERE id = @id;";

        await _db.Database.OpenConnectionAsync(ct);
        try
        {
            await using var cmd = _db.Database.GetDbConnection().CreateCommand();
            cmd.CommandText = sql;

            cmd.Parameters.Add(new NpgsqlParameter("id", id));
            cmd.Parameters.Add(new NpgsqlParameter("status", (status ?? "UNKNOWN").Trim().ToUpperInvariant()));
            cmd.Parameters.Add(new NpgsqlParameter("rejection_reason", (object?)rejectionReason ?? DBNull.Value));
            cmd.Parameters.Add(new NpgsqlParameter("meta_template_id", (object?)metaTemplateId ?? DBNull.Value));

            await cmd.ExecuteNonQueryAsync(ct);
        }
        finally
        {
            await _db.Database.CloseConnectionAsync();
        }
    }

    private static WhatsappTemplateDto Map(System.Data.Common.DbDataReader r)
    {
        var id = r.GetGuid(0);
        var tenantId = r.GetGuid(1);
        var connectionId = r.GetGuid(2);
        var name = r.GetString(3);
        var language = r.GetString(4);
        var category = r.GetString(5);
        var status = r.GetString(6);
        var componentsJson = r.GetString(7);

        var metaTemplateId = r.IsDBNull(8) ? null : r.GetString(8);
        var rejectionReason = r.IsDBNull(9) ? null : r.GetString(9);

        var createdAt = r.GetFieldValue<DateTimeOffset>(10);
        var updatedAt = r.GetFieldValue<DateTimeOffset>(11);

        return new WhatsappTemplateDto(
            Id: id,
            TenantId: tenantId,
            ConnectionId: connectionId,
            Name: name,
            Language: language,
            Category: category,
            Status: status,
            ComponentsJson: componentsJson,
            MetaTemplateId: metaTemplateId,
            RejectionReason: rejectionReason,
            CreatedAtUtc: createdAt,
            UpdatedAtUtc: updatedAt);
    }
}
