using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Profiqo.Application.Abstractions.Persistence;
using Profiqo.Application.Integrations.Ikas;
using Profiqo.Application.Integrations.Jobs;
using Profiqo.Domain.Common.Ids;
using Profiqo.Worker.Tenancy;

namespace Profiqo.Worker.IntegrationJobs;

internal sealed class IntegrationJobWorker : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<IntegrationJobWorker> _logger;
    private readonly string _workerId = $"worker-{Environment.MachineName}-{Guid.NewGuid():N}";

    public IntegrationJobWorker(IServiceProvider sp, ILogger<IntegrationJobWorker> logger)
    {
        _sp = sp;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("IntegrationJobWorker started. workerId={WorkerId}", _workerId);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _sp.CreateScope();

                var tenantSetter = scope.ServiceProvider.GetRequiredService<ITenantContextSetter>();
                var jobs = scope.ServiceProvider.GetRequiredService<IIntegrationJobRepository>();
                var ikas = scope.ServiceProvider.GetRequiredService<IIkasSyncProcessor>();

                var job = await jobs.TryClaimNextAsync(_workerId, stoppingToken);

                if (job is null)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                    continue;
                }

                var tenantId = new TenantId(job.TenantId);
                tenantSetter.Set(tenantId);

                try
                {
                    _logger.LogInformation("Claimed job {JobId} kind={Kind} tenant={Tenant}", job.JobId, job.Kind, job.TenantId);

                    var processed = 0;

                    if (job.Kind == IntegrationJobKind.IkasSyncCustomers)
                    {
                        processed = await ikas.SyncCustomersAsync(job.JobId, tenantId, job.ConnectionId, job.PageSize, job.MaxPages, stoppingToken);
                    }
                    else if (job.Kind == IntegrationJobKind.IkasSyncOrders)
                    {
                        processed = await ikas.SyncOrdersAsync(job.JobId, tenantId, job.ConnectionId, job.PageSize, job.MaxPages, stoppingToken);
                    }
                    else
                    {
                        throw new InvalidOperationException($"Unsupported job kind: {job.Kind}");
                    }

                    // Processor already reports progress per page, keep final mark as well
                    await jobs.MarkProgressAsync(job.JobId, processed, stoppingToken);
                    await jobs.MarkSucceededAsync(job.JobId, stoppingToken);

                    _logger.LogInformation("Job {JobId} succeeded. processed={Processed}", job.JobId, processed);
                }
                catch (Exception ex)
                {
                    await jobs.MarkFailedAsync(job.JobId, ex.Message, stoppingToken);
                    _logger.LogError(ex, "Job {JobId} failed.", job.JobId);
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
}
