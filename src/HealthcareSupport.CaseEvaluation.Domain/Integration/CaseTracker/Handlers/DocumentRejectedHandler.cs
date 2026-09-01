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
/// Withdraws a document from the Case Tracker when staff reject it.
///
/// <para>Sent as a removal, NOT as a <c>Rejected</c> status. A rejection normally follows an
/// acceptance -- staff spotted a problem after the fact -- so their staff should stop seeing a
/// document the portal has repudiated rather than see it flagged. Rejections of never-accepted
/// documents were never published, and removing an id the receiver does not hold is a no-op.</para>
/// </summary>
public class DocumentRejectedHandler :
    DocumentRemovalHandlerBase,
    ILocalEventHandler<AppointmentDocumentRejectedEto>,
    ITransientDependency
{
    public DocumentRejectedHandler(
        IRepository<Appointment, Guid> appointmentRepository,
        ICaseTrackerDocumentQueue documentQueue,
        IClock clock,
        ILogger<DocumentRejectedHandler> logger)
        : base(appointmentRepository, documentQueue, clock, logger)
    {
    }

    [UnitOfWork]
    public virtual Task HandleEventAsync(AppointmentDocumentRejectedEto eventData)
    {
        if (eventData == null)
        {
            return Task.CompletedTask;
        }

        return PublishRemovalAsync(
            eventData.AppointmentId,
            eventData.AppointmentDocumentId,
            nameof(DocumentRejectedHandler));
    }
}
