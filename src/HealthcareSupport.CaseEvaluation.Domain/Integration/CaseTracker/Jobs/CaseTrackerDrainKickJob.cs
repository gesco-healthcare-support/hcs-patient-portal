using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.MultiTenancy;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Uow;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker.Jobs;

/// <summary>
/// Host recurring job that kicks every office's outbox drain every 5 minutes (#917, agreed schedule).
///
/// <para>Why it exists: a drain makes one attempt per due row and does not re-queue itself, so a failed
/// row was only retried when something else happened to start a drain -- in practice the 15-minute
/// reconciliation sweep. The retry waits (5, 10, 20, then 30 minutes) mean nothing unless a drain runs
/// when they expire; without this the shortest recovery stays 15 minutes whatever the waits say.</para>
///
/// <para>Only enqueues; the drain runs out of band, one pass per office at a time (the drain job skips
/// when a pass is already running), so a kick that overlaps the reconciliation sweep's own kick is
/// harmless.</para>
/// </summary>
public class CaseTrackerDrainKickJob : ITransientDependency
{
    public const string RecurringJobId = "case-tracker-drain-kick";

    public const string CronExpression = "*/5 * * * *";

    private readonly ITenantWorkRunner _tenantWorkRunner;
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly ILogger<CaseTrackerDrainKickJob> _logger;

    public CaseTrackerDrainKickJob(
        ITenantWorkRunner tenantWorkRunner,
        IBackgroundJobManager backgroundJobManager,
        ILogger<CaseTrackerDrainKickJob> logger)
    {
        _tenantWorkRunner = tenantWorkRunner;
        _backgroundJobManager = backgroundJobManager;
        _logger = logger;
    }

    // A unit of work for the office-registry read only, as on the reconciliation sweep; the drains
    // themselves run later, in their own jobs.
    [UnitOfWork]
    public virtual async Task ExecuteAsync()
    {
        var kicked = 0;

        await _tenantWorkRunner.ForEachOfficeAsync(async officeId =>
        {
            try
            {
                await _backgroundJobManager.EnqueueAsync(new IntegrationOutboxDrainArgs { TenantId = officeId });
                kicked++;
            }
            catch (Exception ex)
            {
                // Per-office isolation: ForEachOfficeAsync aborts the whole run if a delegate throws.
                _logger.LogError(
                    ex,
                    "CaseTrackerDrainKickJob: could not enqueue the drain for office {OfficeId}; continuing.",
                    officeId);
            }
        });

        _logger.LogDebug("CaseTrackerDrainKickJob: kicked {Count} office drain(s).", kicked);
    }
}
