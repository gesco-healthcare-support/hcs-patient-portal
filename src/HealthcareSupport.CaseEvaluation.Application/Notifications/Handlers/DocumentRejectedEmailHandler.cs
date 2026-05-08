using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace HealthcareSupport.CaseEvaluation.Notifications.Handlers;

/// <summary>
/// Phase 14b (2026-05-04) -- subscribes to
/// <see cref="AppointmentDocumentRejectedEto"/> and dispatches the
/// OLD-parity <c>PatientDocumentRejected</c> email to the original
/// uploader, including the verbatim rejection notes the staff
/// supplied. Mirrors OLD's <c>SendDocumentEmail</c> at
/// <c>P:\PatientPortalOld\PatientAppointment.Domain\AppointmentRequestModule\AppointmentDocumentDomain.cs</c>:273-288.
///
/// <para>The <c>RejectionNotes</c> from the Eto are passed through to
/// the template via the <c>##RejectionNotes##</c> variable -- the
/// seeded body wraps them in OLD's verbatim
/// "&lt;b&gt; Rejection Reason: &lt;/b&gt; {notes}" markup.</para>
/// </summary>
public class DocumentRejectedEmailHandler :
    ILocalEventHandler<AppointmentDocumentRejectedEto>,
    ITransientDependency
{
    private readonly INotificationDispatcher _dispatcher;
    private readonly DocumentEmailContextResolver _contextResolver;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<DocumentRejectedEmailHandler> _logger;

    public DocumentRejectedEmailHandler(
        INotificationDispatcher dispatcher,
        DocumentEmailContextResolver contextResolver,
        ICurrentTenant currentTenant,
        ILogger<DocumentRejectedEmailHandler> logger)
    {
        _dispatcher = dispatcher;
        _contextResolver = contextResolver;
        _currentTenant = currentTenant;
        _logger = logger;
    }

    [UnitOfWork]
    public virtual async Task HandleEventAsync(AppointmentDocumentRejectedEto eventData)
    {
        if (eventData == null)
        {
            return;
        }

        using (_currentTenant.Change(eventData.TenantId))
        {
            var ctx = await _contextResolver.ResolveAsync(eventData.AppointmentId, eventData.AppointmentDocumentId);
            if (ctx == null)
            {
                _logger.LogWarning(
                    "DocumentRejectedEmailHandler: appointment {AppointmentId} not found; skipping.",
                    eventData.AppointmentId);
                return;
            }

            var uploaderEmail = await _contextResolver.ResolveUploaderEmailAsync(
                ctx.DocumentUploadedByUserId,
                ctx.PatientEmail ?? ctx.BookerEmail);

            if (string.IsNullOrWhiteSpace(uploaderEmail))
            {
                _logger.LogInformation(
                    "DocumentRejectedEmailHandler: no uploader email resolved for document {DocumentId} on appointment {AppointmentId}; skipping.",
                    eventData.AppointmentDocumentId,
                    eventData.AppointmentId);
                return;
            }

            var recipients = new List<NotificationRecipient>
            {
                new NotificationRecipient(
                    email: uploaderEmail!,
                    role: RecipientRole.Patient,
                    isRegistered: ctx.DocumentUploadedByUserId.HasValue),
            };

            var variables = DocumentNotificationContext.BuildVariables(
                patientFirstName: ctx.PatientFirstName,
                patientLastName: ctx.PatientLastName,
                patientEmail: ctx.PatientEmail,
                requestConfirmationNumber: ctx.RequestConfirmationNumber,
                appointmentDate: ctx.AppointmentDate,
                claimNumber: ctx.ClaimNumber,
                wcabAdj: ctx.WcabAdj,
                documentName: ctx.DocumentName,
                rejectionNotes: eventData.RejectionNotes,
                clinicName: _currentTenant.Name,
                portalUrl: ctx.PortalBaseUrl);

            await _dispatcher.DispatchAsync(
                templateCode: NotificationTemplateConsts.Codes.PatientDocumentRejected,
                recipients: recipients,
                variables: variables,
                contextTag: $"DocumentRejected/{eventData.AppointmentDocumentId}");
        }
    }
}
