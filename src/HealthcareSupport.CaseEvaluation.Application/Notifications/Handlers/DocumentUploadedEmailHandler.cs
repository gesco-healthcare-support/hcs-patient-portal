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
/// <see cref="AppointmentDocumentUploadedEto"/> and dispatches the
/// OLD-parity <c>PatientDocumentUploaded</c> /
/// <c>PatientNewDocumentUploaded</c> email to the uploader + the
/// appointment's <c>PrimaryResponsibleUserId</c>. Mirrors OLD's
/// <c>SendDocumentEmail</c> at
/// <c>P:\PatientPortalOld\PatientAppointment.Domain\AppointmentRequestModule\AppointmentDocumentDomain.cs</c>:289-303
/// (package-doc upload) and the parallel <c>AppointmentNewDocumentDomain.SendDocumentEmail</c>
/// for ad-hoc uploads.
///
/// <para>Template-code routing per (IsAdHoc, IsJointDeclaration):</para>
/// <list type="bullet">
///   <item>(false, false) -> <c>PatientDocumentUploaded</c></item>
///   <item>(true, false) -> <c>PatientDocumentUploaded</c> (OLD shares
///         the same template enum across the two tables; per
///         <c>docs/parity/external-user-appointment-ad-hoc-documents.md</c>
///         "Email templates shared with package docs")</item>
///   <item>(*, true) -> <c>PatientDocumentUploaded</c> (JDF reuses
///         the same template surface; subject builder includes
///         "Joint Declaration" via the document name)</item>
/// </list>
///
/// <para>Responsible-user email skip when <c>PrimaryResponsibleUserId</c>
/// is null (OLD-bug-fix per the ad-hoc audit -- OLD's <c>.Value</c>
/// access NRE'd in that case).</para>
/// </summary>
public class DocumentUploadedEmailHandler :
    ILocalEventHandler<AppointmentDocumentUploadedEto>,
    ITransientDependency
{
    private readonly INotificationDispatcher _dispatcher;
    private readonly DocumentEmailContextResolver _contextResolver;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<DocumentUploadedEmailHandler> _logger;

    public DocumentUploadedEmailHandler(
        INotificationDispatcher dispatcher,
        DocumentEmailContextResolver contextResolver,
        ICurrentTenant currentTenant,
        ILogger<DocumentUploadedEmailHandler> logger)
    {
        _dispatcher = dispatcher;
        _contextResolver = contextResolver;
        _currentTenant = currentTenant;
        _logger = logger;
    }

    [UnitOfWork]
    public virtual async Task HandleEventAsync(AppointmentDocumentUploadedEto eventData)
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
                    "DocumentUploadedEmailHandler: appointment {AppointmentId} not found; skipping.",
                    eventData.AppointmentId);
                return;
            }

            var uploaderEmail = await _contextResolver.ResolveUploaderEmailAsync(
                eventData.UploadedByUserId ?? ctx.DocumentUploadedByUserId,
                ctx.PatientEmail ?? ctx.BookerEmail);

            var responsibleEmail = await _contextResolver.ResolveResponsibleUserEmailAsync(ctx.ResponsibleUserId);

            var recipients = new List<NotificationRecipient>();
            if (!string.IsNullOrWhiteSpace(uploaderEmail))
            {
                recipients.Add(new NotificationRecipient(
                    email: uploaderEmail!,
                    role: RecipientRole.Patient,
                    isRegistered: eventData.UploadedByUserId.HasValue));
            }
            if (!string.IsNullOrWhiteSpace(responsibleEmail))
            {
                recipients.Add(new NotificationRecipient(
                    email: responsibleEmail!,
                    role: RecipientRole.OfficeAdmin,
                    isRegistered: true));
            }

            if (recipients.Count == 0)
            {
                _logger.LogInformation(
                    "DocumentUploadedEmailHandler: no recipients resolved for document {DocumentId} on appointment {AppointmentId}; skipping.",
                    eventData.AppointmentDocumentId,
                    eventData.AppointmentId);
                return;
            }

            var variables = DocumentNotificationContext.BuildVariables(
                patientFirstName: ctx.PatientFirstName,
                patientLastName: ctx.PatientLastName,
                patientEmail: ctx.PatientEmail,
                requestConfirmationNumber: ctx.RequestConfirmationNumber,
                appointmentDate: ctx.AppointmentDate,
                claimNumber: ctx.ClaimNumber,
                wcabAdj: ctx.WcabAdj,
                documentName: ctx.DocumentName,
                rejectionNotes: null,
                clinicName: _currentTenant.Name,
                portalUrl: ctx.PortalBaseUrl);

            // Phase 6.B (Adrian Decision 6.1, 2026-05-08): pick template
            // by (IsAdHoc, IsJointDeclaration) -- 3 OLD-parity paths.
            var templateCode = DocumentNotificationContext.ClassifyDocumentTemplateCode(
                DocumentEmailKind.Uploaded,
                ctx.IsAdHoc,
                ctx.IsJointDeclaration);

            await _dispatcher.DispatchAsync(
                templateCode: templateCode,
                recipients: recipients,
                variables: variables,
                contextTag: $"DocumentUploaded/{templateCode}/{eventData.AppointmentDocumentId}");
        }
    }
}
