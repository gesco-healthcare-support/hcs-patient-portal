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
///
/// <para>Phase 5 (Category 5, 2026-05-10) adds the
/// <c>PatientDocumentRejectedRemainingDocs</c> branch: when the
/// rejected document is a package doc (<c>!IsAdHoc</c> AND
/// <c>!IsJointDeclaration</c>) and the appointment still has Pending
/// package docs, dispatch the RemainingDocs template with
/// <c>##RemainingDocumentCount##</c> + <c>##URL##</c> variables.</para>
/// </summary>
public class DocumentRejectedEmailHandler :
    ILocalEventHandler<AppointmentDocumentRejectedEto>,
    ITransientDependency
{
    private readonly DocumentEmailFlow _flow;

    public DocumentRejectedEmailHandler(
        INotificationDispatcher dispatcher,
        DocumentEmailContextResolver contextResolver,
        IAppointmentRecipientResolver recipientResolver,
        MissingRequiredDocumentsResolver missingRequiredDocumentsResolver,
        ICurrentTenant currentTenant,
        ILogger<DocumentRejectedEmailHandler> logger,
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
    public virtual async Task HandleEventAsync(AppointmentDocumentRejectedEto eventData)
    {
        if (eventData == null)
        {
            return;
        }

        await _flow.SendAsync(new DocumentEmailSpec
        {
            HandlerName = nameof(DocumentRejectedEmailHandler),
            ContextTagPrefix = "DocumentRejected",
            TenantId = eventData.TenantId,
            AppointmentId = eventData.AppointmentId,
            AppointmentDocumentId = eventData.AppointmentDocumentId,
            Kind = NotificationKind.DocumentRejected,
            EmailKind = DocumentEmailKind.Rejected,
            ExcludeOfficeFromCc = true,
            RejectionNotes = eventData.RejectionNotes,
            Uploader = ctx => (ctx.DocumentUploadedByUserId, ctx.DocumentUploadedByUserId.HasValue),
            RemainingDocsTemplateCode = NotificationTemplateConsts.Codes.PatientDocumentRejectedRemainingDocs,
        });
    }
}
