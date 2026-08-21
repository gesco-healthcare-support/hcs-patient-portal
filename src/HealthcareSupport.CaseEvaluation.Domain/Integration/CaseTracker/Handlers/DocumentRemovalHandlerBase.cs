using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using Microsoft.Extensions.Logging;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Timing;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker.Handlers;

/// <summary>
/// Shared removal path for the two routes that take a document away from the Case Tracker: staff
/// rejecting one they had accepted, and IT-Admin deleting one. Both publish the identical tombstone,
/// because from the receiver's side a repudiated document and a deleted one are the same instruction:
/// stop showing it.
///
/// <para>A base class rather than a collaborator because the two handlers differ ONLY in which event
/// they subscribe to -- there is no second decision to compose.</para>
/// </summary>
public abstract class DocumentRemovalHandlerBase
{
    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly ICaseTrackerDocumentQueue _documentQueue;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    protected DocumentRemovalHandlerBase(
        IRepository<Appointment, Guid> appointmentRepository,
        ICaseTrackerDocumentQueue documentQueue,
        IClock clock,
        ILogger logger)
    {
        _appointmentRepository = appointmentRepository;
        _documentQueue = documentQueue;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Queues a tombstone for one document. Never throws: the staff action that triggered it has
    /// already succeeded and must not be undone by an integration failure.
    ///
    /// <para>Deliberately does NOT check whether the document was ever published. Deleting an id the
    /// receiver does not hold is a harmless no-op, so tracking published-state would duplicate the
    /// outbox ledger to prevent nothing. It also could not work here: by the time this runs the row
    /// is soft-deleted, so a lookup would come back empty.</para>
    /// </summary>
    protected async Task PublishRemovalAsync(Guid appointmentId, Guid documentId, string trigger)
    {
        try
        {
            var appointment = await _appointmentRepository.FindAsync(appointmentId);
            if (appointment == null)
            {
                _logger.LogWarning(
                    "{Trigger}: appointment {AppointmentId} not found; no tombstone published.",
                    trigger, appointmentId);
                return;
            }

            if (!CaseTrackerPublishPolicy.ShouldPublish(appointment.AppointmentStatus))
            {
                _logger.LogDebug(
                    "{Trigger}: appointment {AppointmentId} is {Status} and was never pushed; no tombstone needed.",
                    trigger, appointmentId, appointment.AppointmentStatus);
                return;
            }

            var entry = new DocumentDeletionEntry
            {
                Id = documentId,
                UpdatedAt = IntegrationTimestamp.ToIsoUtc(_clock.Now),
            };

            var row = await _documentQueue.EnqueueDeletionsAsync(
                appointmentId, appointment.TenantId, new[] { entry });

            _logger.LogInformation(
                "{Trigger}: document {DocumentId} on appointment {AppointmentId} queued for removal (row {RowId}).",
                trigger, documentId, appointmentId, row?.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "{Trigger}: failed to queue removal of document {DocumentId} on appointment {AppointmentId}; the staff action stands.",
                trigger, documentId, appointmentId);
        }
    }
}
