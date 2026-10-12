using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace HealthcareSupport.CaseEvaluation.Notifications.Handlers;

/// <summary>
/// Phase 14b (2026-05-04) -- subscribes to
/// <see cref="AppointmentDocumentUploadedEto"/> and dispatches ONE email:
/// To the uploader, CC every other party the recipient resolver returns.
/// The office mailbox is NOT filtered out of the CC here (staff want a copy
/// of new uploads; the accept and reject emails drop it), and the dispatcher
/// drops a CC equal to the To. Mirrors OLD's
/// <c>SendDocumentEmail</c> at
/// <c>P:\PatientPortalOld\PatientAppointment.Domain\AppointmentRequestModule\AppointmentDocumentDomain.cs</c>:289-303
/// (package-doc upload) and the parallel <c>AppointmentNewDocumentDomain.SendDocumentEmail</c>
/// for ad-hoc uploads.
///
/// <para>The uploader is the event's <c>UploadedByUserId</c>, else the
/// document's stored uploader, with the patient email (else the booker
/// email) as the fallback address -- an anonymous verification-code upload
/// has no user id. The To is "registered" only when the EVENT carries a
/// user id.</para>
///
/// <para>Template-code routing per (IsAdHoc, IsJointDeclaration), via
/// <see cref="DocumentNotificationContext.ClassifyDocumentTemplateCode"/>;
/// the Joint Declaration flag wins over ad-hoc:</para>
/// <list type="bullet">
///   <item>(false, false) -> <c>PatientDocumentUploaded</c></item>
///   <item>(true, false) -> <c>PatientNewDocumentUploaded</c></item>
///   <item>(*, true) -> <c>JointAgreementLetterUploaded</c></item>
/// </list>
///
/// <para>There is no separate responsible-user
/// (<c>PrimaryResponsibleUserId</c>) recipient.</para>
/// </summary>
public class DocumentUploadedEmailHandler :
    ILocalEventHandler<AppointmentDocumentUploadedEto>,
    ITransientDependency
{
    private readonly DocumentEmailFlow _flow;

    public DocumentUploadedEmailHandler(
        INotificationDispatcher dispatcher,
        DocumentEmailContextResolver contextResolver,
        IAppointmentRecipientResolver recipientResolver,
        ICurrentTenant currentTenant,
        ILogger<DocumentUploadedEmailHandler> logger,
        IAccountUrlBuilder accountUrlBuilder)
    {
        _flow = new DocumentEmailFlow(
            dispatcher,
            contextResolver,
            recipientResolver,
            null,
            currentTenant,
            logger,
            accountUrlBuilder);
    }

    [UnitOfWork]
    public virtual async Task HandleEventAsync(AppointmentDocumentUploadedEto eventData)
    {
        if (eventData == null)
        {
            return;
        }

        await _flow.SendAsync(new DocumentEmailSpec
        {
            HandlerName = nameof(DocumentUploadedEmailHandler),
            ContextTagPrefix = "DocumentUploaded",
            TenantId = eventData.TenantId,
            AppointmentId = eventData.AppointmentId,
            AppointmentDocumentId = eventData.AppointmentDocumentId,
            Kind = NotificationKind.DocumentUploaded,
            EmailKind = DocumentEmailKind.Uploaded,
            ExcludeOfficeFromCc = false,
            Uploader = ctx => (eventData.UploadedByUserId ?? ctx.DocumentUploadedByUserId, eventData.UploadedByUserId.HasValue),
        });
    }
}
