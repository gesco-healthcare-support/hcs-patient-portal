using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.MultiTenancy;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Timing;
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

    /// <summary>
    /// How far back the sweep looks, against <c>LastModificationTime ?? CreationTime</c>.
    ///
    /// <para>REQUIRED, not a tuning knob. Without a floor this query matches every appointment that
    /// predates the integration, since none of them has an intake row -- so the sweep would enqueue
    /// the entire history of every office, and enabling an office would flush all of it to the Case
    /// Tracker as fresh intakes (including long-cancelled appointments, because
    /// <see cref="CaseTrackerPublishPolicy.IsPublished"/> excludes only Pending / Rejected /
    /// InfoRequested). Their side turns an intake into a case, so that would mean duplicate cases for
    /// work their staff already created by hand.</para>
    ///
    /// <para>Seven days is generous for what this job actually targets: an enqueue that threw minutes
    /// to hours ago. Approval stamps <c>LastModificationTime</c>, so a recently approved appointment
    /// is inside the window while untouched history is not. A backfill is a deliberate,
    /// one-off decision -- not something an hourly sweep should perform as a side effect.</para>
    /// </summary>
    public const int LookbackDays = 7;

    private readonly ITenantWorkRunner _tenantWorkRunner;
    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly IRepository<AppointmentPacket, Guid> _packetRepository;
    private readonly IIntegrationOutboxRepository _outboxRepository;
    private readonly ICaseTrackerIntakeQueue _intakeQueue;
    private readonly IClock _clock;
    private readonly ILogger<CaseTrackerCompletenessSweepJob> _logger;

    public CaseTrackerCompletenessSweepJob(
        ITenantWorkRunner tenantWorkRunner,
        IRepository<Appointment, Guid> appointmentRepository,
        IRepository<AppointmentPacket, Guid> packetRepository,
        IIntegrationOutboxRepository outboxRepository,
        ICaseTrackerIntakeQueue intakeQueue,
        IClock clock,
        ILogger<CaseTrackerCompletenessSweepJob> logger)
    {
        _tenantWorkRunner = tenantWorkRunner;
        _appointmentRepository = appointmentRepository;
        _packetRepository = packetRepository;
        _outboxRepository = outboxRepository;
        _intakeQueue = intakeQueue;
        _clock = clock;
        _logger = logger;
    }

    [UnitOfWork]
    public virtual async Task ExecuteAsync()
    {
        var offices = 0;
        var recovered = 0;
        var cutoffUtc = _clock.Now.AddDays(-LookbackDays);

        await _tenantWorkRunner.ForEachOfficeAsync(async officeId =>
        {
            offices++;
            try
            {
                recovered += await SweepOfficeAsync(officeId, cutoffUtc);
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

        // The window is logged, not implied: a pass that found nothing because everything was outside
        // the lookback reads identically to a pass that found nothing because all is well.
        _logger.LogInformation(
            "CaseTrackerCompletenessSweepJob: swept {OfficeCount} offices, recovered {Recovered} missing intake row(s); window = changed since {CutoffUtc} (last {LookbackDays} day(s)).",
            offices, recovered, cutoffUtc, LookbackDays);
    }

    private async Task<int> SweepOfficeAsync(Guid officeId, DateTime cutoffUtc)
    {
        var missing = await FindAppointmentsWithoutIntakeAsync(cutoffUtc);
        if (missing.Count == 0)
        {
            return 0;
        }

        var settleCutoff = PacketSetPolicy.Cutoff(_clock.Now);
        var recovered = 0;

        foreach (var appointment in missing)
        {
            // Phase 4d (2026-08-05), REMOVED IN 4E. Without this arm the sweep undoes the other two
            // gates within the hour: a replacement appointment is published, settled and has no
            // intake row, which is precisely this job's definition of a lost enqueue.
            if (CaseTrackerRescheduleSuppressionPolicy.IsSuppressed(appointment))
            {
                _logger.LogDebug(
                    "CaseTrackerCompletenessSweepJob: appointment {AppointmentId} has no intake row by design; it is one half of a phase-4d reschedule split and stays off the wire until 4e amends the contract (status {Status}, rescheduled-from {SourceId}).",
                    appointment.Id, appointment.AppointmentStatus, appointment.RescheduledFromAppointmentId);
                continue;
            }

            // Since 2026-07-30 "published with no intake row" is the NORMAL state between approval and
            // the packet set settling, so recovering unconditionally would race the deferral and send
            // exactly the packet-less intake it exists to prevent. Waiting for settle keeps this job
            // what it is meant to be -- a backstop for an enqueue that was genuinely lost -- and the
            // settle cutoff doubles as its grace period, so there is no second timing knob.
            var packets = await _packetRepository.GetListAsync(p => p.AppointmentId == appointment.Id);
            if (!IntakeSettlePolicy.IsSettled(appointment, packets, settleCutoff))
            {
                _logger.LogDebug(
                    "CaseTrackerCompletenessSweepJob: appointment {AppointmentId} has no intake row yet, but its packet set is still settling; leaving it to the settle path.",
                    appointment.Id);
                continue;
            }

            // Enqueue rather than push. Re-enqueueing something that DOES have a row is harmless anyway:
            // the idempotency key is versioned by the appointment's UpdatedAt, so a duplicate collapses
            // onto the existing row instead of double-sending.
            await _intakeQueue.EnqueueIntakeAsync(appointment.Id, appointment.TenantId);
            recovered++;

            _logger.LogWarning(
                "CaseTrackerCompletenessSweepJob: appointment {AppointmentId} in office {OfficeId} is {Status} with a settled packet set and no intake row; enqueued one. Its original enqueue was lost.",
                appointment.Id, officeId, appointment.AppointmentStatus);
        }

        return recovered;
    }

    /// <summary>
    /// Published appointments changed within <see cref="LookbackDays"/> that have no
    /// <see cref="IntegrationMessageType.Intake"/> row in ANY state -- Pending, Sent, Failed or
    /// Resolved all count as "a row exists", because the point is to catch the case where nothing was
    /// ever written.
    ///
    /// <para><c>IsPublished</c> is applied in memory after materialising, because it is a C# switch
    /// rather than a translatable expression. The lookback filter is deliberately applied BEFORE that
    /// -- in the database -- so an office with years of history does not have all of it pulled into
    /// memory on every hourly run.</para>
    /// </summary>
    private async Task<System.Collections.Generic.List<Appointment>> FindAppointmentsWithoutIntakeAsync(
        DateTime cutoffUtc)
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
            .Where(a => (a.LastModificationTime ?? a.CreationTime) >= cutoffUtc)
            .ToList()
            .Where(a => CaseTrackerPublishPolicy.IsPublished(a.AppointmentStatus))
            .Take(BatchSize)
            .ToList();
    }
}
