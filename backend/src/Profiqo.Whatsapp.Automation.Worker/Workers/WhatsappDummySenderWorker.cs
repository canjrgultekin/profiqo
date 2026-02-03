using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using Profiqo.Application.Abstractions.Persistence.WhatsappAutomation;
using Profiqo.Domain.Common.Ids;
using Profiqo.Infrastructure.Persistence;
using Profiqo.Infrastructure.Persistence.Entities;
using Profiqo.Whatsapp.Automation.Worker.Tenancy;

namespace Profiqo.Whatsapp.Automation.Worker.Workers;

public sealed class WhatsappDummySenderWorker : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<WhatsappDummySenderWorker> _logger;
    private readonly WhatsappAutomationOptions _opt;

    private readonly string _workerId = $"wa-auto-{Environment.MachineName}-{Guid.NewGuid():N}";

    public WhatsappDummySenderWorker(IServiceProvider sp, ILogger<WhatsappDummySenderWorker> logger, IOptions<WhatsappAutomationOptions> opt)
    {
        _sp = sp;
        _logger = logger;
        _opt = opt.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("WhatsappDummySenderWorker started. workerId={WorkerId}", _workerId);

        var lastSweep = DateTimeOffset.MinValue;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _sp.CreateScope();

                var db = scope.ServiceProvider.GetRequiredService<ProfiqoDbContext>();
                var tenantSetter = scope.ServiceProvider.GetRequiredService<ITenantContextSetter>();
                var dispatch = scope.ServiceProvider.GetRequiredService<IWhatsappDispatchRepository>();

                var now = DateTimeOffset.UtcNow;

                if ((now - lastSweep).TotalSeconds >= Math.Max(5, _opt.StaleSweepEverySeconds))
                {
                    var released = await dispatch.ReleaseStaleLocksAsync(TimeSpan.FromSeconds(Math.Max(10, _opt.LockTtlSeconds)), stoppingToken);
                    if (released > 0) _logger.LogWarning("Released stale locks: {Count}", released);
                    lastSweep = now;
                }

                var claimed = await dispatch.TryClaimNextAsync(_workerId, now, stoppingToken);
                if (claimed is null)
                {
                    await Task.Delay(Math.Max(150, _opt.SenderPollMs), stoppingToken);
                    continue;
                }

                tenantSetter.Set(new TenantId(claimed.Value.TenantId));

                try
                {
                    // Rule daily limit al
                    var rule = await db.Set<WhatsappRuleRow>()
                        .AsNoTracking()
                        .FirstOrDefaultAsync(x => x.Id == claimed.Value.RuleId && x.TenantId == claimed.Value.TenantId, stoppingToken);

                    var limit = rule?.DailyLimit is 2 ? (short)2 : (short)1;

                    var allowed = await dispatch.TryConsumeDailyQuotaAsync(claimed.Value.TenantId, claimed.Value.CustomerId, claimed.Value.LocalDate, limit, stoppingToken);
                    if (!allowed)
                    {
                        await dispatch.MarkSuppressedLimitAsync(claimed.Value.Id, "Suppressed: daily limit reached", stoppingToken);
                        continue;
                    }

                    // Dummy send success
                    await dispatch.MarkSentDummyAsync(claimed.Value.Id, stoppingToken);
                }
                catch (Exception ex)
                {
                    var next = ComputeNextAttemptUtc(claimed.Value.AttemptCount);
                    var msg = Trunc(ex.Message, 7800);

                    if (claimed.Value.AttemptCount + 1 >= _opt.MaxAttempts)
                    {
                        await dispatch.MarkFailedAsync(claimed.Value.Id, $"FAILED(permanent): {msg}", DateTimeOffset.UtcNow.AddYears(10), stoppingToken);
                    }
                    else
                    {
                        await dispatch.MarkFailedAsync(claimed.Value.Id, msg, next, stoppingToken);
                    }
                }
                finally
                {
                    tenantSetter.Clear();
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sender loop error.");
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
    }

    private DateTimeOffset ComputeNextAttemptUtc(int attemptCount)
    {
        var a = Math.Max(0, attemptCount);
        var baseSec = Math.Max(1, _opt.BaseRetrySeconds);

        var sec = baseSec * Math.Pow(2, Math.Min(a, 10));
        sec = Math.Min(sec, Math.Max(baseSec, _opt.MaxRetrySeconds));

        var jitter = Random.Shared.Next(0, baseSec);
        return DateTimeOffset.UtcNow.AddSeconds(sec + jitter);
    }

    private static string Trunc(string s, int max)
        => string.IsNullOrEmpty(s) ? s : (s.Length <= max ? s : s[..max]);
}
