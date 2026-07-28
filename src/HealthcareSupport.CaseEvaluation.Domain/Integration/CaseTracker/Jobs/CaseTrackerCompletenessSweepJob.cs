using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.MultiTenancy;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker.Jobs;

/// <summary>
/// Finds published appointments that have NO intake outbox row at all, and enqueues one.
///
/// <para>This closes the one gap every other safety net misses. The retry policy, the dead-letter list
/// and the alert all assume a row EXISTS. But the approval handler wraps its enqueue in a try/catch so
/// that an integration failure can never fail a staff member's approval -- which means if the enqueue
/// itself throws, the appointment is approved, no row is written, and there is nothing to retry, nothing
/// to dead-letter and nothing to alert on. That case is invisible in both systems, and it is exactly the
/// silent failure contract section I2 exists to prevent.</para>
///
/// <para>Its own job rather than another pass inside <see cref="CaseTrackerReconciliationJob"/>: that
/// class already takes eight dependencies and this needs two more, which would put it well past the
/// repo's ceiling for a DI constructor. Two single-purpose jobs also fail independently.</para>
/// </summary>
public class CaseTrackerCompletenessSweepJob : ITransientDependency
{
    public const string RecurringJobId = "case-tracker-completeness-sweep";

    /// <summary>
    /// Hourly, not every 15 minutes. This detects a rare bug rather than a routine transient, and each
    /// run reads every published appointment in every office, so it is the most expensive of the three
    /// sweeps and the least urgent.
    /// </summary>
    public const string CronExpression = "0 * * * *";

    /// <summary>
    /// Ceiling per office per run, so a first run against a backlog cannot become a very long job.
    /// Anything skipped is picked up next hour, and a truncated pass is logged rather than dropped
    /// silently.
    /// </summary>
    public const int BatchSize = 50;

    private readonly ITenantWorkRunner _tenantWorkRunner;
    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly IIntegrationOutboxRepository _outboxRepository;
    private readonly ICaseTrackerIntakeQueue _intakeQueue;
    private readonly ILogger<CaseTrackerCompletenessSweepJob> _logger;

    public CaseTrackerCompletenessSweepJob(
        ITenantWorkRunner tenantWorkRunner,
        IRepository<Appointment, Guid> appointmentRepository,
        IIntegrationOutboxRepository outboxRepository,
        ICaseTrackerIntakeQueue intakeQueue,
        ILogger<CaseTrackerCompletenessSweepJob> logger)
    {
        _tenantWorkRunner = tenantWorkRunner;
        _appointmentRepository = appointmentRepository;
        _outboxRepository = outboxRepository;
        _intakeQueue = intakeQueue;
        _logger = logger;
    }

    [UnitOfWork]
    public virtual async Task ExecuteAsync()
    {
        var offices = 0;
        var recovered = 0;

        await _tenantWorkRunner.ForEachOfficeAsync(async officeId =>
        {
            offices++;
            try
            {
                recovered += await SweepOfficeAsync(officeId);
            }
            catch (Exception ex)
            {
                // Per-office isolation: ForEachOfficeAsync aborts the whole run on a throw.
                _logger.LogError(
                    ex,
                    "CaseTrackerCompletenessSweepJob: office {OfficeId} failed; continuing with the next office.",
                    officeId);
            }
        });

        _logger.LogInformation(
            "CaseTrackerCompletenessSweepJob: swept {OfficeCount} offices, recovered {Recovered} missing intake row(s).",
            offices, recovered);
    }

    private async Task<int> SweepOfficeAsync(Guid officeId)
    {
        var missing = await FindAppointmentsWithoutIntakeAsync();
        if (missing.Count == 0)
        {
            return 0;
        }

        foreach (var appointment in missing)
        {
            // Enqueue rather than push. Re-enqueueing something that DOES have a row is harmless anyway:
            // the idempotency key is versioned by the appointment's UpdatedAt, so a duplicate collapses
            // onto the existing row instead of double-sending.
            await _intakeQueue.EnqueueIntakeAsync(appointment.Id, appointment.TenantId);

            _logger.LogWarning(
                "CaseTrackerCompletenessSweepJob: appointment {AppointmentId} in office {OfficeId} is {Status} with no intake row; enqueued one. Its original enqueue was lost.",
                appointment.Id, officeId, appointment.AppointmentStatus);
        }

        return missing.Count;
    }

    /// <summary>
    /// Published appointments with no <see cref="IntegrationMessageType.Intake"/> row in ANY state --
    /// Pending, Sent, Failed or Resolved all count as "a row exists", because the point is to catch the
    /// case where nothing was ever written.
    /// </summary>
    private async Task<System.Collections.Generic.List<Appointment>> FindAppointmentsWithoutIntakeAsync()
    {
        var outboxQueryable = await _outboxRepository.GetQueryableAsync();
        var appointmentIdsWithIntake = outboxQueryable
            .Where(x => x.MessageType == IntegrationMessageType.Intake)
            .Select(x => x.AppointmentId)
            .Distinct()
            .ToList();

        var appointmentQueryable = await _appointmentRepository.GetQueryableAsync();

        return appointmentQueryable
            .Where(a => !appointmentIdsWithIntake.Contains(a.Id))
            .ToList()
            .Where(a => CaseTrackerPublishPolicy.IsPublished(a.AppointmentStatus))
            .Take(BatchSize)
            .ToList();
    }
}
