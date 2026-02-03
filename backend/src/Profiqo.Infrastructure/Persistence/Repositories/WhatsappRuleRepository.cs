using Microsoft.EntityFrameworkCore;

using Profiqo.Application.Abstractions.Persistence.WhatsappAutomation;
using Profiqo.Infrastructure.Persistence.Entities;

namespace Profiqo.Infrastructure.Persistence.Repositories;

internal sealed class WhatsappRuleRepository : IWhatsappRuleRepository
{
    private readonly ProfiqoDbContext _db;
    public WhatsappRuleRepository(ProfiqoDbContext db) => _db = db;

    public async Task<IReadOnlyList<WhatsappRuleDto>> ListAsync(Guid tenantId, CancellationToken ct)
    {
        var rows = await _db.Set<WhatsappRuleRow>()
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.UpdatedAtUtc)
            .ToListAsync(ct);

        return rows.Select(ToDto).ToList();
    }

    public async Task<WhatsappRuleDto?> GetAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        var row = await _db.Set<WhatsappRuleRow>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, ct);

        return row is null ? null : ToDto(row);
    }

    public async Task<Guid> UpsertAsync(WhatsappRuleDto dto, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        var row = await _db.Set<WhatsappRuleRow>()
            .FirstOrDefaultAsync(x => x.TenantId == dto.TenantId && x.Id == dto.Id, ct);

        if (row is null)
        {
            row = new WhatsappRuleRow
            {
                Id = dto.Id == Guid.Empty ? Guid.NewGuid() : dto.Id,
                TenantId = dto.TenantId,
                CreatedAtUtc = now
            };
            await _db.AddAsync(row, ct);
        }

        row.Name = (dto.Name ?? "").Trim();
        row.Mode = dto.Mode;
        row.DailyLimit = dto.DailyLimit is 2 ? (short)2 : (short)1;
        row.Timezone = string.IsNullOrWhiteSpace(dto.Timezone) ? "Europe/Istanbul" : dto.Timezone.Trim();

        row.DailyTime1 = dto.DailyTime1;
        row.DailyTime2 = dto.DailyTime2;
        row.DailyDelay2Minutes = dto.DailyDelay2Minutes;

        row.OrderDelay1Minutes = dto.OrderDelay1Minutes;
        row.OrderDelay2Minutes = dto.OrderDelay2Minutes;

        row.IsActive = dto.IsActive;
        row.UpdatedAtUtc = now;

        await _db.SaveChangesAsync(ct);
        return row.Id;
    }

    public async Task DeleteAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        var row = await _db.Set<WhatsappRuleRow>().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, ct);
        if (row is null) return;

        _db.Remove(row);
        await _db.SaveChangesAsync(ct);
    }

    private static WhatsappRuleDto ToDto(WhatsappRuleRow r)
        => new(r.Id, r.TenantId, r.Name, r.Mode, r.DailyLimit, r.Timezone, r.DailyTime1, r.DailyTime2, r.DailyDelay2Minutes, r.OrderDelay1Minutes, r.OrderDelay2Minutes, r.IsActive, r.CreatedAtUtc, r.UpdatedAtUtc);
}
