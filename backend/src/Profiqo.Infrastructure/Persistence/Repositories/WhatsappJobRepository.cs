using Microsoft.EntityFrameworkCore;

using Profiqo.Application.Abstractions.Persistence.WhatsappAutomation;
using Profiqo.Infrastructure.Persistence.Entities;

namespace Profiqo.Infrastructure.Persistence.Repositories;

internal sealed class WhatsappJobRepository : IWhatsappJobRepository
{
    private readonly ProfiqoDbContext _db;
    public WhatsappJobRepository(ProfiqoDbContext db) => _db = db;

    public async Task<IReadOnlyList<WhatsappJobDto>> ListAsync(Guid tenantId, CancellationToken ct)
    {
        var rows = await _db.Set<WhatsappJobRow>()
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.UpdatedAtUtc)
            .ToListAsync(ct);

        return rows.Select(ToDto).ToList();
    }

    public async Task<WhatsappJobDto?> GetAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        var row = await _db.Set<WhatsappJobRow>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, ct);

        return row is null ? null : ToDto(row);
    }

    public async Task<Guid> UpsertAsync(WhatsappJobDto dto, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        var row = await _db.Set<WhatsappJobRow>()
            .FirstOrDefaultAsync(x => x.TenantId == dto.TenantId && x.Id == dto.Id, ct);

        if (row is null)
        {
            row = new WhatsappJobRow
            {
                Id = dto.Id == Guid.Empty ? Guid.NewGuid() : dto.Id,
                TenantId = dto.TenantId,
                CreatedAtUtc = now
            };
            await _db.AddAsync(row, ct);
        }

        row.Name = (dto.Name ?? "").Trim();
        row.RuleId = dto.RuleId;
        row.Template1Id = dto.Template1Id;
        row.Template2Id = dto.Template2Id;
        row.TargetsJson = EnsureTargetsJson(dto.TargetsJson);
        row.IsActive = dto.IsActive;
        row.UpdatedAtUtc = now;

        await _db.SaveChangesAsync(ct);
        return row.Id;
    }

    public async Task SetActiveAsync(Guid tenantId, Guid id, bool isActive, CancellationToken ct)
    {
        var row = await _db.Set<WhatsappJobRow>().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, ct);
        if (row is null) return;

        row.IsActive = isActive;
        row.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        var row = await _db.Set<WhatsappJobRow>().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, ct);
        if (row is null) return;

        _db.Remove(row);
        await _db.SaveChangesAsync(ct);
    }

    private static WhatsappJobDto ToDto(WhatsappJobRow r)
        => new(r.Id, r.TenantId, r.Name, r.RuleId, r.Template1Id, r.Template2Id, r.TargetsJson, r.IsActive, r.CreatedAtUtc, r.UpdatedAtUtc);

    private static string EnsureTargetsJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return "[]";

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Array)
                throw new ArgumentException("targets must be JSON array");
            return json.Trim();
        }
        catch
        {
            throw new ArgumentException("Invalid targets JSON.");
        }
    }
}
