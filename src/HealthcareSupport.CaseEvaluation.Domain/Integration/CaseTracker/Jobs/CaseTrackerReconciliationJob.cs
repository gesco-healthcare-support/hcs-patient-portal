using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.MultiTenancy;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Uow;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker.Jobs;

/// <summary>
/// Host recurring sweep that kicks each office's outbox drain. The backstop for the one loss window
/// the prompt path cannot close on its own: a drain enqueue lost to a crash between the approval
/// commit and Hangfire accepting the job. The row is already committed, so re-driving the drain
/// recovers it.
///
/// <para>Also the mechanism that resumes delivery after the push is switched ON for an office: rows
/// accumulated while it was disabled are still Pending and simply drain on the next sweep.</para>
/// </summary>
public class CaseTrackerReconciliationJob : ITransientDependency
{
    private readonly ITenantWorkRunner _tenantWorkRunner;
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly ILogger<CaseTrackerReconciliationJob> _logger;

    public CaseTrackerReconciliationJob(
        ITenantWorkRunner tenantWorkRunner,
        IBackgroundJobManager backgroundJobManager,
        ILogger<CaseTrackerReconciliationJob> logger)
    {
        _tenantWorkRunner = tenantWorkRunner;
        _backgroundJobManager = backgroundJobManager;
        _logger = logger;
    }

    public const string RecurringJobId = "case-tracker-reconciliation";

    /// <summary>Every 15 minutes -- a backstop cadence, matching the approval reconciliation sweep.</summary>
    public const string CronExpression = "*/15 * * * *";

    [UnitOfWork]
    public virtual async Task ExecuteAsync()
    {
        var offices = 0;
        var failures = 0;

        await _tenantWorkRunner.ForEachOfficeAsync(async officeId =>
        {
            offices++;
            try
            {
                // Out-of-band: do not block the sweep on HTTP to the Case Tracker.
                await _backgroundJobManager.EnqueueAsync(new IntegrationOutboxDrainArgs { TenantId = officeId });
            }
            catch (Exception ex)
            {
                // Per-office isolation: ForEachOfficeAsync aborts the WHOLE sweep if a delegate
                // throws, so one bad office must not stop the rest.
                failures++;
                _logger.LogError(
                    ex,
                    "CaseTrackerReconciliationJob: office {OfficeId} drain enqueue failed; continuing with the next office.",
                    officeId);
            }
        });

        _logger.LogInformation(
            "CaseTrackerReconciliationJob: swept {OfficeCount} offices ({Failures} failed).",
            offices, failures);
    }
}
