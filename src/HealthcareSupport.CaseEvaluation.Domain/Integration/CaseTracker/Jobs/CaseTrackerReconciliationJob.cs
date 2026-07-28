using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.MultiTenancy;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker.Jobs;

/// <summary>
/// Host recurring sweep with two jobs, both backstops for losses the prompt path cannot close alone:
///
/// <para>1. Kicks each office's outbox drain, recovering a drain enqueue lost to a crash between the
/// commit and Hangfire accepting the job. The row is already committed, so re-driving finds it. This
/// is also what resumes delivery after the push is switched ON for an office -- rows that accumulated
/// while it was disabled are still Pending and simply drain.</para>
///
/// <para>2. Releases stalled packet sets. <c>PacketsCompleteHandler</c> deliberately publishes nothing
/// until all kinds render, so one permanently failed template would withhold the others forever. This
/// publishes whatever DID generate once the set has stopped moving.</para>
/// </summary>
public class CaseTrackerReconciliationJob : ITransientDependency
{
    private readonly ITenantWorkRunner _tenantWorkRunner;
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly IRepository<AppointmentPacket, Guid> _packetRepository;
    private readonly IDocumentListResolver _documentListResolver;
    private readonly ICaseTrackerDocumentQueue _documentQueue;
    private readonly IClock _clock;
    private readonly ILogger<CaseTrackerReconciliationJob> _logger;

    public CaseTrackerReconciliationJob(
        ITenantWorkRunner tenantWorkRunner,
        IBackgroundJobManager backgroundJobManager,
        IRepository<Appointment, Guid> appointmentRepository,
        IRepository<AppointmentPacket, Guid> packetRepository,
        IDocumentListResolver documentListResolver,
        ICaseTrackerDocumentQueue documentQueue,
        IClock clock,
        ILogger<CaseTrackerReconciliationJob> logger)
    {
        _tenantWorkRunner = tenantWorkRunner;
        _backgroundJobManager = backgroundJobManager;
        _appointmentRepository = appointmentRepository;
        _packetRepository = packetRepository;
        _documentListResolver = documentListResolver;
        _documentQueue = documentQueue;
        _clock = clock;
        _logger = logger;
    }

    public const string RecurringJobId = "case-tracker-reconciliation";

    /// <summary>Every 15 minutes -- a backstop cadence, matching the approval reconciliation sweep.</summary>
    public const string CronExpression = "*/15 * * * *";

    /// <summary>
    /// How long a packet set must sit unchanged before the generated kinds are released without it.
    /// Comfortably longer than a normal render (seconds to a couple of minutes) so a slow job is never
    /// mistaken for a stuck one, but short enough that their staff are not waiting on a dead template.
    /// </summary>
    public const int PacketReleaseAfterMinutes = 30;

    /// <summary>
    /// Ceiling on releases per office per sweep, so a backlog cannot turn one sweep into a long-running
    /// job. Anything skipped is picked up 15 minutes later; a truncated pass is logged rather than
    /// silently dropped.
    /// </summary>
    public const int PacketReleaseBatchSize = 50;

    [UnitOfWork]
    public virtual async Task ExecuteAsync()
    {
        var offices = 0;
        var failures = 0;
        var released = 0;

        await _tenantWorkRunner.ForEachOfficeAsync(async officeId =>
        {
            offices++;
            try
            {
                // Release first, so anything it enqueues drains in this same pass.
                released += await ReleaseStalledPacketSetsAsync(officeId);

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
                    "CaseTrackerReconciliationJob: office {OfficeId} failed; continuing with the next office.",
                    officeId);
            }
        });

        _logger.LogInformation(
            "CaseTrackerReconciliationJob: swept {OfficeCount} offices ({Failures} failed, {Released} packet sets released).",
            offices, failures, released);
    }

    /// <summary>
    /// Publishes the generated kinds for appointments whose packet set has stopped moving while still
    /// incomplete. Runs inside the office's own database, scoped by
    /// <see cref="ITenantWorkRunner.ForEachOfficeAsync"/>.
    ///
    /// <para>Re-running is safe and expected: the entry set is unchanged between sweeps, so the outbox
    /// idempotency key matches and the enqueue collapses onto the existing row. Once the missing kind
    /// eventually renders, the set changes and a fresh push goes out.</para>
    /// </summary>
    private async Task<int> ReleaseStalledPacketSetsAsync(Guid officeId)
    {
        var cutoff = _clock.Now.AddMinutes(-PacketReleaseAfterMinutes);

        var packetQueryable = await _packetRepository.GetQueryableAsync();
        var candidateIds = packetQueryable
            .Where(p => p.Status != PacketGenerationStatus.Generated)
            .Where(p => (p.LastModificationTime ?? p.CreationTime) < cutoff)
            .Select(p => p.AppointmentId)
            .Distinct()
            .Take(PacketReleaseBatchSize + 1)
            .ToList();

        var truncated = candidateIds.Count > PacketReleaseBatchSize;
        if (truncated)
        {
            candidateIds = candidateIds.Take(PacketReleaseBatchSize).ToList();
            // One placeholder per name: a repeated name misbinds the structured properties (S6677).
            _logger.LogWarning(
                "CaseTrackerReconciliationJob: office {OfficeId} has more than {BatchSize} stalled packet sets; releasing that many now and deferring the rest to the next sweep.",
                officeId, PacketReleaseBatchSize);
        }

        var released = 0;
        foreach (var appointmentId in candidateIds)
        {
            if (await TryReleaseAsync(appointmentId, cutoff))
            {
                released++;
            }
        }

        return released;
    }

    private async Task<bool> TryReleaseAsync(Guid appointmentId, DateTime cutoff)
    {
        var packets = await _packetRepository.GetListAsync(p => p.AppointmentId == appointmentId);
        if (!PacketSetPolicy.ShouldRelease(packets, cutoff))
        {
            return false;
        }

        var appointment = await _appointmentRepository.FindAsync(appointmentId);
        if (appointment == null || !CaseTrackerPublishPolicy.IsPublished(appointment.AppointmentStatus))
        {
            return false;
        }

        var entries = await _documentListResolver.ResolvePacketsAsync(appointment);
        if (entries.Count == 0)
        {
            return false;
        }

        await _documentQueue.EnqueueDocumentEntriesAsync(appointmentId, appointment.TenantId, entries);

        _logger.LogInformation(
            "CaseTrackerReconciliationJob: released {Count} generated packet(s) for appointment {AppointmentId}; the set has been incomplete since before {Cutoff:o}.",
            entries.Count, appointmentId, cutoff);

        return true;
    }
}
