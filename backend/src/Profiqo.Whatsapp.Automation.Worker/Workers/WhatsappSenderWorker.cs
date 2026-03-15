using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using Profiqo.Application.Abstractions.Crypto;
using Profiqo.Application.Abstractions.Integrations.Whatsapp;
using Profiqo.Application.Abstractions.Persistence.Whatsapp;
using Profiqo.Application.Abstractions.Persistence.WhatsappAutomation;
using Profiqo.Application.Integrations.Whatsapp;
using Profiqo.Domain.Common.Ids;
using Profiqo.Domain.Integrations;
using Profiqo.Infrastructure.Persistence;
using Profiqo.Infrastructure.Persistence.Entities;
using Profiqo.Whatsapp.Automation.Worker.Tenancy;

namespace Profiqo.Whatsapp.Automation.Worker.Workers;

public sealed class WhatsappSenderWorker : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<WhatsappSenderWorker> _logger;
    private readonly WhatsappAutomationOptions _opt;
    private readonly WhatsappIntegrationOptions _whOpt;

    private readonly string _workerId = $"wa-sender-{Environment.MachineName}-{Guid.NewGuid():N}";

    public WhatsappSenderWorker(
        IServiceProvider sp,
        ILogger<WhatsappSenderWorker> logger,
        IOptions<WhatsappAutomationOptions> opt,
        IOptions<WhatsappIntegrationOptions> whOpt)
    {
        _sp = sp;
        _logger = logger;
        _opt = opt.Value;
        _whOpt = whOpt.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("WhatsappSenderWorker started. workerId={WorkerId}", _workerId);

        var lastSweep = DateTimeOffset.MinValue;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _sp.CreateScope();

                var db = scope.ServiceProvider.GetRequiredService<ProfiqoDbContext>();
                var tenantSetter = scope.ServiceProvider.GetRequiredService<ITenantContextSetter>();
                var dispatch = scope.ServiceProvider.GetRequiredService<IWhatsappDispatchRepository>();

                var secrets = scope.ServiceProvider.GetRequiredService<ISecretProtector>();
                var templateRepo = scope.ServiceProvider.GetRequiredService<IWhatsappTemplateRepository>();
                var cloudSender = scope.ServiceProvider.GetRequiredService<WhatsappCloudSender>();

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
                    // Rule limit
                    var rule = await db.Set<WhatsappRuleRow>()
                        .AsNoTracking()
                        .FirstOrDefaultAsync(x => x.Id == claimed.Value.RuleId && x.TenantId == claimed.Value.TenantId, stoppingToken);

                    var limit = rule?.DailyLimit is 2 ? (short)2 : (short)1;

                    // quota
                    var allowed = await dispatch.TryConsumeDailyQuotaAsync(claimed.Value.TenantId, claimed.Value.CustomerId, claimed.Value.LocalDate, limit, stoppingToken);
                    if (!allowed)
                    {
                        await dispatch.MarkSuppressedLimitAsync(claimed.Value.Id, "Suppressed: daily limit reached", stoppingToken);
                        continue;
                    }

                    // tenant whatsapp connection
                    var conn = await db.Set<ProviderConnection>()
                        .AsNoTracking()
                        .FirstOrDefaultAsync(x => x.TenantId.Value == claimed.Value.TenantId && x.ProviderType == ProviderType.Whatsapp, stoppingToken);

                    var effectiveTestMode =
                        _whOpt.ForceTestMode ||
                        conn is null ||
                        conn.IsTestMode ||
                        conn.Status != ProviderConnectionStatus.Active;

                    if (effectiveTestMode)
                    {
                        await dispatch.MarkSentAsync(claimed.Value.Id, true, stoppingToken);
                        continue;
                    }

                    // decrypt credential secret
                    WhatsappCredentialSecret cred;
                    try
                    {
                        var json = secrets.Unprotect(conn!.AccessToken);
                        cred = WhatsappCredentialSecret.FromJson(json);
                    }
                    catch
                    {
                        await dispatch.MarkSentAsync(claimed.Value.Id, true, stoppingToken);
                        continue;
                    }

                    // template resolve
                    var tpl = await templateRepo.GetByIdAsync(claimed.Value.TemplateId, stoppingToken);
                    if (tpl is null)
                    {
                        await dispatch.MarkFailedAsync(claimed.Value.Id, "Template not found", now.AddMinutes(5), stoppingToken);
                        continue;
                    }

                    // Real send (template param yoksa çalışır)
                    await cloudSender.SendTemplateAsync(
                        accessToken: cred.AccessToken,
                        phoneNumberId: cred.PhoneNumberId,
                        toE164: claimed.Value.ToE164,
                        templateName: tpl.Name,
                        languageCode: tpl.Language,
                        ct: stoppingToken);

                    await dispatch.MarkSentAsync(claimed.Value.Id, false, stoppingToken);
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
