using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
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
/// <see cref="AppointmentDocumentAcceptedEto"/> and dispatches the
/// OLD-parity <c>PatientDocumentAccepted</c> email to the original
/// uploader (or the booker email as fallback when the upload was
/// anonymous via verification code). Mirrors OLD's
/// <c>SendDocumentEmail</c> at
/// <c>P:\PatientPortalOld\PatientAppointment.Domain\AppointmentRequestModule\AppointmentDocumentDomain.cs</c>:256-272.
///
/// <para>Phase 5 (Category 5, 2026-05-10) adds the
/// <c>PatientDocumentAcceptedRemainingDocs</c> branch: when the accepted
/// document is a package doc (<c>!IsAdHoc</c> AND
/// <c>!IsJointDeclaration</c>) and the appointment still has Pending
/// package docs, dispatch the RemainingDocs template with
/// <c>##RemainingDocumentCount##</c> + <c>##URL##</c> variables. This is
/// a NEW UX improvement -- OLD scaffolded the templates but only used
/// them in <c>AppointmentJointDeclarationDomain.cs</c>:244, a code path
/// the audit confirms is effectively dead.</para>
/// </summary>
public class DocumentAcceptedEmailHandler :
    ILocalEventHandler<AppointmentDocumentAcceptedEto>,
    ITransientDependency
{
    private readonly DocumentEmailFlow _flow;

    public DocumentAcceptedEmailHandler(
        INotificationDispatcher dispatcher,
        DocumentEmailContextResolver contextResolver,
        IAppointmentRecipientResolver recipientResolver,
        MissingRequiredDocumentsResolver missingRequiredDocumentsResolver,
        ICurrentTenant currentTenant,
        ILogger<DocumentAcceptedEmailHandler> logger,
        IAccountUrlBuilder accountUrlBuilder)
    {
        _flow = new DocumentEmailFlow(
            dispatcher,
            contextResolver,
            recipientResolver,
            missingRequiredDocumentsResolver,
            currentTenant,
            logger,
            accountUrlBuilder);
    }

    [UnitOfWork]
    public virtual async Task HandleEventAsync(AppointmentDocumentAcceptedEto eventData)
    {
        if (eventData == null)
        {
            return;
        }

        await _flow.SendAsync(new DocumentEmailSpec
        {
            HandlerName = nameof(DocumentAcceptedEmailHandler),
            ContextTagPrefix = "DocumentAccepted",
            TenantId = eventData.TenantId,
            AppointmentId = eventData.AppointmentId,
            AppointmentDocumentId = eventData.AppointmentDocumentId,
            Kind = NotificationKind.DocumentAccepted,
            EmailKind = DocumentEmailKind.Accepted,
            ExcludeOfficeFromCc = true,
            Uploader = ctx => (ctx.DocumentUploadedByUserId, ctx.DocumentUploadedByUserId.HasValue),
            RemainingDocsTemplateCode = NotificationTemplateConsts.Codes.PatientDocumentAcceptedRemainingDocs,
        });
    }
}
