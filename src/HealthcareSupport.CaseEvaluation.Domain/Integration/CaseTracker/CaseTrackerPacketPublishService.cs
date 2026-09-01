using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Publishes an appointment's settled packet set the RIGHT way round: as the appointment's first
/// intake if the Case Tracker has never heard of it, otherwise as a document update.
///
/// <para>Shared by the per-packet handler and the reconciliation release so the two cannot disagree
/// about which message a settled set becomes -- the same reason
/// <see cref="PacketSetPolicy"/> is pure and shared.</para>
///
/// <para>Why the branch exists: since 2026-07-30 the intake waits for packets, so the settle moment
/// is normally an appointment's FIRST contact and the packets belong in the intake's own
/// <c>documents</c> array. But packets can also settle a second time -- a regenerated packet, or a
/// stalled kind that finally renders after the intake already went -- and then the intake is history
/// and the change belongs on the document feed. Sending an intake in that case would be a second
/// full-appointment push to deliver one file.</para>
/// </summary>
public class CaseTrackerPacketPublishService : ITransientDependency
{
    private readonly IIntegrationOutboxRepository _outboxRepository;
    private readonly ICaseTrackerIntakeQueue _intakeQueue;
    private readonly ICaseTrackerDocumentQueue _documentQueue;
    private readonly IDocumentListResolver _documentListResolver;
    private readonly ILogger<CaseTrackerPacketPublishService> _logger;

    public CaseTrackerPacketPublishService(
        IIntegrationOutboxRepository outboxRepository,
        ICaseTrackerIntakeQueue intakeQueue,
        ICaseTrackerDocumentQueue documentQueue,
        IDocumentListResolver documentListResolver,
        ILogger<CaseTrackerPacketPublishService> logger)
    {
        _outboxRepository = outboxRepository;
        _intakeQueue = intakeQueue;
        _documentQueue = documentQueue;
        _documentListResolver = documentListResolver;
        _logger = logger;
    }

    /// <summary>
    /// Publishes the appointment's packets. Returns true when something was enqueued.
    ///
    /// <para>Callers must already have decided the set is worth publishing -- this method does not
    /// re-check completeness, so the per-packet handler and the stalled release can apply their own
    /// (different) rules.</para>
    /// </summary>
    public virtual async Task<bool> PublishSettledPacketsAsync(
        Appointment appointment,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(appointment);

        if (!await HasIntakeAsync(appointment.Id, cancellationToken))
        {
            // First contact: the packets ride along inside the intake, which the payload builder
            // assembles from the same resolver, so no entry list is needed here.
            var row = await _intakeQueue.EnqueueIntakeAsync(
                appointment.Id, appointment.TenantId, cancellationToken);

            _logger.LogInformation(
                "CaseTrackerPacketPublishService: appointment {AppointmentId} packets settled and became its first intake (row {RowId}).",
                appointment.Id, row.Id);

            return true;
        }

        var entries = await _documentListResolver.ResolvePacketsAsync(appointment, cancellationToken);
        if (entries.Count == 0)
        {
            _logger.LogDebug(
                "CaseTrackerPacketPublishService: appointment {AppointmentId} already has an intake and no fetchable packets; nothing published.",
                appointment.Id);
            return false;
        }

        var documentRow = await _documentQueue.EnqueueDocumentEntriesAsync(
            appointment.Id, appointment.TenantId, entries, cancellationToken);

        _logger.LogInformation(
            "CaseTrackerPacketPublishService: appointment {AppointmentId} packets changed after its intake; {Count} entry(s) queued as a document update (row {RowId}).",
            appointment.Id, entries.Count, documentRow?.Id);

        return documentRow != null;
    }

    /// <summary>
    /// Whether this appointment has EVER had an intake row, in any state. Pending, Sent, Failed and
    /// Resolved all count: the question is "has the Case Tracker been told about this appointment",
    /// and a Failed or still-Pending row means the telling is already in hand and retrying is the
    /// outbox's job, not a reason to queue a second intake.
    /// </summary>
    private async Task<bool> HasIntakeAsync(Guid appointmentId, CancellationToken cancellationToken)
    {
        var queryable = await _outboxRepository.GetQueryableAsync();

        return queryable.Any(x =>
            x.AppointmentId == appointmentId &&
            x.MessageType == IntegrationMessageType.Intake);
    }
}
