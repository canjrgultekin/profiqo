using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using Profiqo.Application.Abstractions.Persistence.WhatsappAutomation;
using Profiqo.Domain.Common.Ids;
using Profiqo.Infrastructure.Persistence;
using Profiqo.Infrastructure.Persistence.Entities;
using Profiqo.Whatsapp.Automation.Worker.Tenancy;

namespace Profiqo.Whatsapp.Automation.Worker.Workers;

public sealed class WhatsappSchedulerWorker : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<WhatsappSchedulerWorker> _logger;
    private readonly WhatsappAutomationOptions _opt;

    public WhatsappSchedulerWorker(IServiceProvider sp, ILogger<WhatsappSchedulerWorker> logger, IOptions<WhatsappAutomationOptions> opt)
    {
        _sp = sp;
        _logger = logger;
        _opt = opt.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("WhatsappSchedulerWorker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ProfiqoDbContext>();
                var tenantSetter = scope.ServiceProvider.GetRequiredService<ITenantContextSetter>();
                var dispatch = scope.ServiceProvider.GetRequiredService<IWhatsappDispatchRepository>();

                // Cross-tenant active jobs scan (read-only)
                var activeJobs = await db.Set<WhatsappJobRow>()
                    .AsNoTracking()
                    .Where(x => x.IsActive)
                    .ToListAsync(stoppingToken);

                if (activeJobs.Count == 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(_opt.SchedulerIntervalSeconds), stoppingToken);
                    continue;
                }

                // Rules cache (tenant-scope)
                var ruleIds = activeJobs.Select(x => x.RuleId).Distinct().ToList();
                var rules = await db.Set<WhatsappRuleRow>()
                    .AsNoTracking()
                    .Where(x => ruleIds.Contains(x.Id))
                    .ToListAsync(stoppingToken);

                var rulesById = rules.ToDictionary(x => x.Id, x => x);

                // 1) Daily rules dispatch üret
                foreach (var job in activeJobs)
                {
                    if (!rulesById.TryGetValue(job.RuleId, out var rule)) continue;
                    if (!rule.IsActive) continue;

                    if (rule.Mode == WhatsappRuleMode.Daily)
                    {
                        tenantSetter.Set(new TenantId(job.TenantId));

                        try
                        {
                            var tz = TimeZones.Istanbul;
                            var localNow = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz);
                            var localDate = DateOnly.FromDateTime(localNow.DateTime);

                            if (rule.DailyTime1 is null) continue;

                            var planned1Utc = ToUtc(localDate, rule.DailyTime1.Value, tz);
                            if (planned1Utc < DateTimeOffset.UtcNow.AddMinutes(-2))
                            {
                                // bugün geçmişse dokunma, yarın scheduler basar
                                continue;
                            }

                            var targets = ParseTargets(job.TargetsJson);

                            foreach (var t in targets)
                            {
                                var payload1 = JsonSerializer.Serialize(new { kind = "daily", ruleId = rule.Id, jobId = job.Id, templateId = job.Template1Id, messageNo = 1 });
                                _ = await dispatch.TryEnqueueUniqueAsync(job.TenantId, job.Id, rule.Id, t.customerId, t.to, 1, job.Template1Id, planned1Utc, localDate, payload1, stoppingToken);

                                if (rule.DailyLimit >= 2 && job.Template2Id.HasValue)
                                {
                                    var planned2Utc = ComputeDailySecond(planned1Utc, localDate, tz, rule);
                                    if (planned2Utc.HasValue)
                                    {
                                        var payload2 = JsonSerializer.Serialize(new { kind = "daily", ruleId = rule.Id, jobId = job.Id, templateId = job.Template2Id.Value, messageNo = 2 });
                                        _ = await dispatch.TryEnqueueUniqueAsync(job.TenantId, job.Id, rule.Id, t.customerId, t.to, 2, job.Template2Id.Value, planned2Utc.Value, localDate, payload2, stoppingToken);
                                    }
                                }
                            }
                        }
                        finally
                        {
                            tenantSetter.Clear();
                        }
                    }
                }

                // 2) Order events oku ve order rules ile dispatch üret
                var unprocessed = await db.Set<WhatsappOrderEventRow>()
                    .AsNoTracking()
                    .Where(x => x.ProcessedAtUtc == null)
                    .OrderBy(x => x.OccurredAtUtc)
                    .Take(200)
                    .ToListAsync(stoppingToken);

                if (unprocessed.Count > 0)
                {
                    foreach (var ev in unprocessed)
                    {
                        // bu tenant'ın order-event mode joblarını bul
                        var tenantJobs = activeJobs.Where(j => j.TenantId == ev.TenantId).ToList();
                        if (tenantJobs.Count == 0) continue;

                        tenantSetter.Set(new TenantId(ev.TenantId));
                        try
                        {
                            foreach (var job in tenantJobs)
                            {
                                if (!rulesById.TryGetValue(job.RuleId, out var rule)) continue;
                                if (!rule.IsActive) continue;
                                if (rule.Mode != WhatsappRuleMode.OrderEvent) continue;

                                var d1 = Math.Max(0, rule.OrderDelay1Minutes ?? 0);
                                var planned1 = ev.OccurredAtUtc.AddMinutes(d1);

                                var payload1 = JsonSerializer.Serialize(new { kind = "order", orderId = ev.OrderId, ruleId = rule.Id, jobId = job.Id, templateId = job.Template1Id, messageNo = 1 });
                                _ = await dispatch.TryEnqueueUniqueAsync(ev.TenantId, job.Id, rule.Id, ev.CustomerId, ev.ToE164, 1, job.Template1Id, planned1, DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(ev.OccurredAtUtc, TimeZones.Istanbul).DateTime), payload1, stoppingToken);

                                if (rule.DailyLimit >= 2 && job.Template2Id.HasValue)
                                {
                                    var d2 = Math.Max(1, rule.OrderDelay2Minutes ?? 60);
                                    var planned2 = planned1.AddMinutes(d2);

                                    var payload2 = JsonSerializer.Serialize(new { kind = "order", orderId = ev.OrderId, ruleId = rule.Id, jobId = job.Id, templateId = job.Template2Id.Value, messageNo = 2 });
                                    _ = await dispatch.TryEnqueueUniqueAsync(ev.TenantId, job.Id, rule.Id, ev.CustomerId, ev.ToE164, 2, job.Template2Id.Value, planned2, DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(ev.OccurredAtUtc, TimeZones.Istanbul).DateTime), payload2, stoppingToken);
                                }
                            }

                            // event processed işaretle (raw SQL daha güvenli)
                            await db.Database.ExecuteSqlRawAsync(
                                "UPDATE whatsapp_order_events SET processed_at_utc = now() WHERE id = {0}",
                                new object[] { ev.Id },
                                stoppingToken);
                        }
                        finally
                        {
                            tenantSetter.Clear();
                        }
                    }
                }

                await Task.Delay(TimeSpan.FromSeconds(_opt.SchedulerIntervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scheduler loop error.");
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
    }

    private static DateTimeOffset ToUtc(DateOnly localDate, TimeOnly localTime, TimeZoneInfo tz)
    {
        var dt = new DateTime(localDate.Year, localDate.Month, localDate.Day, localTime.Hour, localTime.Minute, localTime.Second, DateTimeKind.Unspecified);
        var utc = TimeZoneInfo.ConvertTimeToUtc(dt, tz);
        return new DateTimeOffset(utc, TimeSpan.Zero);
    }

    private static DateTimeOffset? ComputeDailySecond(DateTimeOffset planned1Utc, DateOnly localDate, TimeZoneInfo tz, WhatsappRuleRow rule)
    {
        if (rule.DailyTime2 is not null)
        {
            var planned2 = ToUtc(localDate, rule.DailyTime2.Value, tz);
            if (planned2 <= planned1Utc) return null;
            return planned2;
        }

        if (rule.DailyDelay2Minutes is not null && rule.DailyDelay2Minutes.Value > 0)
            return planned1Utc.AddMinutes(rule.DailyDelay2Minutes.Value);

        return null;
    }

    private static List<(Guid customerId, string to)> ParseTargets(string json)
    {
        var list = new List<(Guid, string)>();
        if (string.IsNullOrWhiteSpace(json)) return list;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return list;

            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var cid = Guid.Empty;
                var to = "";

                if (el.TryGetProperty("customerId", out var c) && Guid.TryParse(c.GetString(), out var g))
                    cid = g;

                if (el.TryGetProperty("toE164", out var t))
                    to = t.GetString() ?? "";

                if (cid != Guid.Empty && !string.IsNullOrWhiteSpace(to))
                    list.Add((cid, to));
            }
        }
        catch { }

        return list;
    }
}
