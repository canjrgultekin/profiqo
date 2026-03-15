using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using Profiqo.Api.Security;
using Profiqo.Application.Abstractions.Crypto;
using Profiqo.Application.Abstractions.Tenancy;
using Profiqo.Domain.Common.Ids;
using Profiqo.Domain.Common.Types;
using Profiqo.Domain.Customers;
using Profiqo.Infrastructure.Persistence;
using Profiqo.Infrastructure.Persistence.Entities;

namespace Profiqo.Api.Controllers;

[ApiController]
[Route("api/whatsapp/targets")]
[Authorize(Policy = AuthorizationPolicies.ReportAccess)]
public sealed class WhatsappTargetsController : ControllerBase
{
    private readonly ProfiqoDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ISecretProtector _secrets;

    public WhatsappTargetsController(ProfiqoDbContext db, ITenantContext tenant, ISecretProtector secrets)
    {
        _db = db;
        _tenant = tenant;
        _secrets = secrets;
    }

    public sealed record TargetRow(
        Guid CustomerId,
        string FullName,
        string? PhoneE164,
        DateTimeOffset LastSeenAtUtc);

    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] string? q = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null) return BadRequest(new { message = "Tenant context missing." });

        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        // Merge link seti (source -> canonical)
        var links = _db.Set<CustomerMergeLink>()
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId.Value);

        // Base: her customer için canonical id hesapla
        var baseQuery =
            from c in _db.Customers.AsNoTracking()
            where c.TenantId == tenantId.Value
            join ml in links on c.Id equals ml.SourceCustomerId into mlj
            from ml in mlj.DefaultIfEmpty()
            select new
            {
                Customer = c,
                CanonicalCustomerId = ml != null ? ml.CanonicalCustomerId : c.Id
            };

        // Arama: sadece ad/soyad
        if (!string.IsNullOrWhiteSpace(q))
        {
            var s = q.Trim();
            baseQuery = baseQuery.Where(x =>
                (x.Customer.FirstName != null && EF.Functions.ILike(x.Customer.FirstName, $"%{s}%")) ||
                (x.Customer.LastName != null && EF.Functions.ILike(x.Customer.LastName, $"%{s}%")));
        }

        // Canonical bazında grupla (tekilleşmiş müşteri listesi)
        var grouped =
            baseQuery
                .GroupBy(x => x.CanonicalCustomerId)
                .Select(g => new
                {
                    CanonicalCustomerId = g.Key, // CustomerId
                    LastSeenAtUtc = g.Max(x => x.Customer.LastSeenAtUtc)
                });

        var total = await grouped.CountAsync(ct);

        var pageRows = await grouped
            .OrderByDescending(x => x.LastSeenAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var canonicalCustomerIds = pageRows.Select(x => x.CanonicalCustomerId).ToList(); // List<CustomerId>

        // Canonical müşterilerin isimlerini çek
        var canonCustomers = await _db.Customers.AsNoTracking()
            .Where(x => x.TenantId == tenantId.Value && canonicalCustomerIds.Contains(x.Id))
            .Select(x => new
            {
                customerId = x.Id.Value,
                firstName = x.FirstName,
                lastName = x.LastName
            })
            .ToListAsync(ct);

        var canonName = canonCustomers.ToDictionary(x => x.customerId, x =>
        {
            var n = $"{x.firstName ?? ""} {x.lastName ?? ""}".Trim();
            return string.IsNullOrWhiteSpace(n) ? x.customerId.ToString() : n;
        });

        // canonical için member customerId listesi (merge link + canonical)
        // Not: burada canonicalCustomerIds (CustomerId) ile filter ediyoruz, EF translate sorunsuz.
        var linkRows = await _db.Set<CustomerMergeLink>().AsNoTracking()
            .Where(x => x.TenantId == tenantId.Value && canonicalCustomerIds.Contains(x.CanonicalCustomerId))
            .Select(x => new { sourceId = x.SourceCustomerId.Value, canonicalId = x.CanonicalCustomerId.Value })
            .ToListAsync(ct);

        // source -> canonical map (Guid -> Guid)
        var sourceToCanonical = new Dictionary<Guid, Guid>(linkRows.Count + canonicalCustomerIds.Count);
        foreach (var lr in linkRows) sourceToCanonical[lr.sourceId] = lr.canonicalId;

        foreach (var cid in canonicalCustomerIds)
            sourceToCanonical[cid.Value] = cid.Value; // canonical kendisi

        var memberIds = sourceToCanonical.Keys.ToList(); // List<Guid>
        var memberCustomerIds = memberIds.Select(x => new CustomerId(x)).ToList(); // List<CustomerId> (EF translate için)

        // member müşterilerden phone identity çek
        var phoneCandidates = await _db.Customers.AsNoTracking()
            .Where(c => c.TenantId == tenantId.Value && memberCustomerIds.Contains(c.Id))
            .SelectMany(c => c.Identities
                .Where(i => i.Type == IdentityType.Phone)
                .Select(i => new
                {
                    customerId = c.Id.Value, // Guid
                    lastSeen = i.LastSeenAtUtc,
                    enc = i.ValueEncrypted
                }))
            .ToListAsync(ct);

        // canonical başına en güncel phone
        var bestPhone = new Dictionary<Guid, (DateTimeOffset lastSeen, string phone)>();

        foreach (var p in phoneCandidates)
        {
            if (!sourceToCanonical.TryGetValue(p.customerId, out var canonicalId)) continue;

            var phone = DecryptPhoneSafe(p.enc);
            if (string.IsNullOrWhiteSpace(phone)) continue;

            if (!bestPhone.TryGetValue(canonicalId, out var cur) || p.lastSeen > cur.lastSeen)
                bestPhone[canonicalId] = (p.lastSeen, phone.Trim());
        }

        var items = pageRows.Select(r =>
        {
            var cid = r.CanonicalCustomerId.Value;
            canonName.TryGetValue(cid, out var fullName);

            bestPhone.TryGetValue(cid, out var phone);

            return new TargetRow(
                CustomerId: cid,
                FullName: fullName ?? cid.ToString(),
                PhoneE164: phone.phone,
                LastSeenAtUtc: r.LastSeenAtUtc);
        });

        return Ok(new { page, pageSize, total, items });
    }

    private string? DecryptPhoneSafe(EncryptedSecret? enc)
    {
        if (enc is null) return null;

        try
        {
            var plain = _secrets.Unprotect(enc);
            return string.IsNullOrWhiteSpace(plain) ? null : plain.Trim();
        }
        catch
        {
            return null;
        }
    }
}
