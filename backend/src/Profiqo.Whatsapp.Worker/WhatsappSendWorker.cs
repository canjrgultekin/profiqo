using System.Text.Json;

using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

using Profiqo.Application.Abstractions.Integrations.Whatsapp;
using Profiqo.Application.Abstractions.Persistence.Whatsapp;
using Profiqo.Domain.Common.Ids;
using Profiqo.Whatsapp.Worker.Tenancy;

namespace Profiqo.Whatsapp.Worker;

public sealed class WhatsappSendWorker : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<WhatsappSendWorker> _logger;
    private readonly WhatsappSendWorkerOptions _opt;

    private readonly string _workerId = $"wa-worker-{Environment.MachineName}-{Guid.NewGuid():N}";

    public WhatsappSendWorker(
        IServiceProvider sp,
        ILogger<WhatsappSendWorker> logger,
        IOptions<WhatsappSendWorkerOptions> opt)
    {
        _sp = sp;
        _logger = logger;
        _opt = opt.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("WhatsappSendWorker started. workerId={WorkerId}", _workerId);

        var lastSweep = DateTimeOffset.MinValue;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _sp.CreateScope();

                var tenantSetter = scope.ServiceProvider.GetRequiredService<ITenantContextSetter>();
                var jobs = scope.ServiceProvider.GetRequiredService<IWhatsappSendJobRepository>();
                var wa = scope.ServiceProvider.GetRequiredService<IWhatsappGraphApiClient>();

                var now = DateTimeOffset.UtcNow;

                if ((now - lastSweep).TotalSeconds >= Math.Max(5, _opt.StaleSweepEverySeconds))
                {
                    var released = await jobs.ReleaseStaleLocksAsync(TimeSpan.FromSeconds(Math.Max(10, _opt.LockTtlSeconds)), stoppingToken);
                    if (released > 0)
                        _logger.LogWarning("Released stale locks: {Count}", released);

                    lastSweep = now;
                }

                var job = await jobs.TryClaimNextAsync(_workerId, now, stoppingToken);

                if (job is null)
                {
                    await Task.Delay(Math.Max(150, _opt.PollIntervalMs), stoppingToken);
                    continue;
                }

                tenantSetter.Set(new TenantId(job.TenantId));

                try
                {
                    var payload = JsonDocument.Parse(job.PayloadJson).RootElement;

                    var phoneNumberId = payload.GetProperty("phoneNumberId").GetString() ?? "";
                    var to = payload.GetProperty("to").GetString() ?? "";
                    var templateName = payload.GetProperty("templateName").GetString() ?? "";
                    var lang = payload.GetProperty("language").GetString() ?? "tr";

                    var useMarketing = payload.TryGetProperty("useMarketingEndpoint", out var me) && me.ValueKind == JsonValueKind.True;

                    JsonElement? components = null;
                    if (payload.TryGetProperty("components", out var comps) && comps.ValueKind == JsonValueKind.Array)
                        components = comps;

                    _logger.LogInformation("Sending job {JobId} tenant={TenantId} to={To} template={Template}",
                        job.Id, job.TenantId, to, templateName);

                    _ = await wa.SendTemplateMessageAsync(
                        phoneNumberId: phoneNumberId,
                        toPhoneE164: to,
                        templateName: templateName,
                        languageCode: lang,
                        components: components,
                        useMarketingEndpoint: useMarketing,
                        ct: stoppingToken);

                    await jobs.MarkSucceededAsync(job.Id, stoppingToken);

                    _logger.LogInformation("Job {JobId} succeeded.", job.Id);
                }
                catch (Exception ex)
                {
                    var nextAttempt = ComputeNextAttemptUtc(job.AttemptCount);
                    var err = Trunc(ex.Message, 7800);

                    if (job.AttemptCount + 1 >= _opt.MaxAttempts)
                    {
                        await jobs.MarkFailedAsync(job.Id, err, stoppingToken);
                        _logger.LogError(ex, "Job {JobId} failed permanently.", job.Id);
                    }
                    else
                    {
                        await jobs.MarkRetryingAsync(job.Id, nextAttempt, err, stoppingToken);
                        _logger.LogWarning(ex, "Job {JobId} will retry at {NextAttempt}.", job.Id, nextAttempt);
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
                _logger.LogError(ex, "Worker loop error.");
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
