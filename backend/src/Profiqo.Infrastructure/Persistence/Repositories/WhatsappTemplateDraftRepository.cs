using Microsoft.EntityFrameworkCore;

using Npgsql;

using Profiqo.Application.Abstractions.Persistence.Whatsapp;
using Profiqo.Application.Abstractions.Persistence.WhatsappAutomation;
using Profiqo.Infrastructure.Persistence;

namespace Profiqo.Infrastructure.Persistence.Repositories;

internal sealed class WhatsappTemplateDraftRepository : IWhatsappTemplateDraftRepository
{
    // Draft şablonları "local connection" olarak tutuyoruz.
    private static readonly Guid LocalConnectionId = Guid.Empty;

    private readonly IWhatsappTemplateRepository _templates;
    private readonly ProfiqoDbContext _db;

    public WhatsappTemplateDraftRepository(IWhatsappTemplateRepository templates, ProfiqoDbContext db)
    {
        _templates = templates;
        _db = db;
    }

    public async Task<IReadOnlyList<WhatsappTemplateDraftDto>> ListAsync(Guid tenantId, CancellationToken ct)
    {
        var list = await _templates.ListAsync(tenantId, LocalConnectionId, ct);
        return list.Select(x => new WhatsappTemplateDraftDto(
            Id: x.Id,
            TenantId: x.TenantId,
            Name: x.Name,
            Language: x.Language,
            Category: x.Category,
            Status: x.Status,
            ComponentsJson: x.ComponentsJson,
            CreatedAtUtc: x.CreatedAtUtc,
            UpdatedAtUtc: x.UpdatedAtUtc)).ToList();
    }

    public async Task<WhatsappTemplateDraftDto?> GetAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        var item = await _templates.GetByIdAsync(id, ct);
        if (item is null) return null;
        if (item.TenantId != tenantId) return null;
        if (item.ConnectionId != LocalConnectionId) return null;

        return new WhatsappTemplateDraftDto(
            Id: item.Id,
            TenantId: item.TenantId,
            Name: item.Name,
            Language: item.Language,
            Category: item.Category,
            Status: item.Status,
            ComponentsJson: item.ComponentsJson,
            CreatedAtUtc: item.CreatedAtUtc,
            UpdatedAtUtc: item.UpdatedAtUtc);
    }

    public async Task<Guid> UpsertAsync(Guid tenantId, Guid? id, string name, string language, string category, string componentsJson, CancellationToken ct)
    {
        // id bazlı upsert istiyorsan (edit), en kolay yöntem name üzerinden ilerlemek.
        // UI zaten edit ederken name’i aynı tutuyor, bu yüzden sorun yok.
        // İstersen id üzerinden update için ayrıca SQL yazabiliriz, ama MVP için name unique yeterli.

        var normalizedName = NormalizeName(name);

        var req = new WhatsappTemplateUpsertRequest(
            TenantId: tenantId,
            ConnectionId: LocalConnectionId,
            Name: normalizedName,
            Language: (language ?? "tr").Trim().ToLowerInvariant(),
            Category: (category ?? "MARKETING").Trim().ToUpperInvariant(),
            Status: "DRAFT",
            ComponentsJson: EnsureArrayJson(componentsJson),
            MetaTemplateId: null,
            RejectionReason: null);

        var newId = await _templates.UpsertAsync(req, ct);
        return newId;
    }

    public async Task DeleteAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        const string sql = @"
DELETE FROM whatsapp_templates
WHERE tenant_id = @tenant_id
  AND connection_id = @connection_id
  AND id = @id;";

        var p1 = new NpgsqlParameter("tenant_id", tenantId);
        var p2 = new NpgsqlParameter("connection_id", LocalConnectionId);
        var p3 = new NpgsqlParameter("id", id);

        await _db.Database.ExecuteSqlRawAsync(sql, new[] { p1, p2, p3 }, ct);
    }

    private static string NormalizeName(string input)
    {
        var s = (input ?? "").Trim().ToLowerInvariant();
        if (s.Length == 0) throw new ArgumentException("Template name required.", nameof(input));

        var chars = new List<char>(s.Length);
        foreach (var ch in s)
        {
            if (ch is >= 'a' and <= 'z') { chars.Add(ch); continue; }
            if (ch is >= '0' and <= '9') { chars.Add(ch); continue; }
            if (ch is '_' or '-' or ' ' or '.') { chars.Add('_'); continue; }
        }

        var raw = new string(chars.ToArray());
        while (raw.Contains("__", StringComparison.Ordinal)) raw = raw.Replace("__", "_", StringComparison.Ordinal);
        raw = raw.Trim('_');
        if (raw.Length == 0) throw new ArgumentException("Template name invalid.", nameof(input));
        if (!(raw[0] is >= 'a' and <= 'z')) raw = "t_" + raw;
        return raw.Length > 512 ? raw[..512] : raw;
    }

    private static string EnsureArrayJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return "[]";
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Array)
            throw new ArgumentException("componentsJson must be JSON array");
        return doc.RootElement.GetRawText();
    }
}
