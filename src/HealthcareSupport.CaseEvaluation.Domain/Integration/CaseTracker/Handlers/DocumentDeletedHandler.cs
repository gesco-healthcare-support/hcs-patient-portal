using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker.Handlers;

/// <summary>
/// Withdraws a document from the Case Tracker when it is deleted in the portal.
///
/// <para>The MinIO object itself is retained -- see
/// <c>AppointmentDocumentsAppService.DeleteAsync</c>. The portal promised the Case Tracker that any
/// key it has been handed stays fetchable, and ABP only soft-deletes the row, so destroying the bytes
/// would have been the one irreversible half of an otherwise reversible operation.</para>
/// </summary>
public class DocumentDeletedHandler :
    DocumentRemovalHandlerBase,
    ILocalEventHandler<AppointmentDocumentDeletedEto>,
    ITransientDependency
{
    public DocumentDeletedHandler(
        IRepository<Appointment, Guid> appointmentRepository,
        ICaseTrackerDocumentQueue documentQueue,
        IClock clock,
        ILogger<DocumentDeletedHandler> logger)
        : base(appointmentRepository, documentQueue, clock, logger)
    {
    }

    [UnitOfWork]
    public virtual Task HandleEventAsync(AppointmentDocumentDeletedEto eventData)
    {
        if (eventData == null)
        {
            return Task.CompletedTask;
        }

        return PublishRemovalAsync(
            eventData.AppointmentId,
            eventData.AppointmentDocumentId,
            nameof(DocumentDeletedHandler));
    }
}
