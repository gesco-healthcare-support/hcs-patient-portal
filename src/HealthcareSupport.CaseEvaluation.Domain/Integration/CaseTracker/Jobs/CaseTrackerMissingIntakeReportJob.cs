using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Local;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker.Jobs;

/// <summary>
/// Weekly, runs the missing-intake report (#944) and, only when some office has a likely-lost intake, raises one
/// event that is emailed to the technical list (decided 2026-09-24: weekly, the technical list, one cross-office
/// email, repeated each week until resolved, silent when there is nothing to report).
///
/// <para>REPORT ONLY, like the reporter it runs: it can publish an event and log, and nothing else. The hourly
/// <see cref="CaseTrackerCompletenessSweepJob"/> recovers a loss inside its 7-day window on its own, so in normal
/// operation this finds nothing and sends nothing. What it catches is older history, and a sweep that has stopped
/// working for longer than its window.</para>
/// </summary>
public class CaseTrackerMissingIntakeReportJob : ITransientDependency
{
    public const string RecurringJobId = "case-tracker-missing-intake-report";

    /// <summary>Mondays at 08:00, Pacific (the recurring-job time zone), before the 09:00 daily digests.</summary>
    public const string CronExpression = "0 8 * * 1";

    private readonly CaseTrackerMissingIntakeReporter _reporter;
    private readonly ILocalEventBus _localEventBus;
    private readonly IClock _clock;
    private readonly ILogger<CaseTrackerMissingIntakeReportJob> _logger;

    public CaseTrackerMissingIntakeReportJob(
        CaseTrackerMissingIntakeReporter reporter,
        ILocalEventBus localEventBus,
        IClock clock,
        ILogger<CaseTrackerMissingIntakeReportJob> logger)
    {
        _reporter = reporter;
        _localEventBus = localEventBus;
        _clock = clock;
        _logger = logger;
    }

    [UnitOfWork]
    public virtual async Task ExecuteAsync()
    {
        var offices = await _reporter.BuildAsync();
        var withLosses = offices.Where(o => o.LikelyLost.Count > 0).ToList();
        var failed = offices.Where(o => o.Failed).ToList();

        // Logged every run: a run that found nothing reads the same as one that never ran, unless it says so.
        _logger.LogInformation(
            "CaseTrackerMissingIntakeReportJob: read {OfficeCount} offices ({FailedCount} could not be read): {LikelyLost} likely lost, {Settling} settling, {BeforeIntegration} before their office's integration.",
            offices.Count, failed.Count,
            offices.Sum(o => o.LikelyLost.Count), offices.Sum(o => o.Settling.Count), offices.Sum(o => o.BeforeIntegrationCount));

        if (withLosses.Count == 0)
        {
            return;
        }

        await _localEventBus.PublishAsync(new CaseTrackerMissingIntakesEto
        {
            RunAt = _clock.Now,
            Offices = withLosses.Select(o => new CaseTrackerMissingIntakesOfficeEto
            {
                TenantId = o.OfficeId,
                OfficeName = o.OfficeName,
                FirstIntakeRowAt = o.FirstIntakeRowAt!.Value,
                Items = o.LikelyLost.Select(i => new CaseTrackerMissingIntakeLineEto
                {
                    AppointmentId = i.AppointmentId,
                    ConfirmationNumber = i.ConfirmationNumber,
                    Status = i.Status,
                    ApprovedAt = i.ApprovedAt,
                }).ToList(),
            }).ToList(),
            FailedOfficeNames = failed.Select(o => o.OfficeName).ToList(),
        });
    }
}
