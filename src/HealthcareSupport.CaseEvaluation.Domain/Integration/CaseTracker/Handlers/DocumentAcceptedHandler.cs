using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus;
using Volo.Abp.Uow;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker.Handlers;

/// <summary>
/// Publishes an uploaded document to the Case Tracker when intake staff accept it.
///
/// <para>Accept is the ONLY upload trigger. An <c>Uploaded</c> document has not been vetted, and the
/// Case Tracker has no use for one their staff might have to un-see -- so unreviewed PHI stays inside
/// the portal until a human has looked at it.</para>
///
/// <para>Lives in Domain, unlike Part 1's approval handler, because the document events are declared
/// in Domain.Shared rather than Application.Contracts.</para>
/// </summary>
public class DocumentAcceptedHandler :
    ILocalEventHandler<AppointmentDocumentAcceptedEto>,
    ITransientDependency
{
    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly IDocumentListResolver _documentListResolver;
    private readonly ICaseTrackerDocumentQueue _documentQueue;
    private readonly ILogger<DocumentAcceptedHandler> _logger;

    public DocumentAcceptedHandler(
        IRepository<Appointment, Guid> appointmentRepository,
        IDocumentListResolver documentListResolver,
        ICaseTrackerDocumentQueue documentQueue,
        ILogger<DocumentAcceptedHandler> logger)
    {
        _appointmentRepository = appointmentRepository;
        _documentListResolver = documentListResolver;
        _documentQueue = documentQueue;
        _logger = logger;
    }

    [UnitOfWork]
    public virtual async Task HandleEventAsync(AppointmentDocumentAcceptedEto eventData)
    {
        if (eventData == null)
        {
            return;
        }

        try
        {
            var appointment = await _appointmentRepository.FindAsync(eventData.AppointmentId);
            if (appointment == null)
            {
                _logger.LogWarning(
                    "DocumentAcceptedHandler: appointment {AppointmentId} not found; nothing published.",
                    eventData.AppointmentId);
                return;
            }

            if (!CaseTrackerPublishPolicy.IsPublished(appointment.AppointmentStatus))
            {
                // No case exists yet. The document is not lost: approval builds the intake payload
                // from the CURRENT document list, so an already-accepted file ships with it.
                _logger.LogDebug(
                    "DocumentAcceptedHandler: appointment {AppointmentId} is {Status}; document {DocumentId} will ship with the intake push instead.",
                    eventData.AppointmentId, appointment.AppointmentStatus, eventData.AppointmentDocumentId);
                return;
            }

            var entry = await _documentListResolver.ResolveDocumentAsync(
                eventData.AppointmentDocumentId, appointment.TenantId);
            if (entry == null)
            {
                _logger.LogWarning(
                    "DocumentAcceptedHandler: document {DocumentId} has no stored object; nothing published.",
                    eventData.AppointmentDocumentId);
                return;
            }

            var row = await _documentQueue.EnqueueDocumentEntriesAsync(
                eventData.AppointmentId, appointment.TenantId, new[] { entry });

            _logger.LogInformation(
                "DocumentAcceptedHandler: document {DocumentId} on appointment {AppointmentId} queued for Case Tracker (row {RowId}).",
                eventData.AppointmentDocumentId, eventData.AppointmentId, row?.Id);
        }
        catch (Exception ex)
        {
            // Accepting a document is the primary business action; the push is downstream of it. The
            // 15-minute sweep and the manual push both re-drive whatever this loses.
            _logger.LogError(
                ex,
                "DocumentAcceptedHandler: failed to queue document {DocumentId} on appointment {AppointmentId}; the acceptance stands.",
                eventData.AppointmentDocumentId, eventData.AppointmentId);
        }
    }
}
