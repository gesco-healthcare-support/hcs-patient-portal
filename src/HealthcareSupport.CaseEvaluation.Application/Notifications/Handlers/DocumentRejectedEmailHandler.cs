using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using HealthcareSupport.CaseEvaluation.Settings;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Settings;
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
    private readonly INotificationDispatcher _dispatcher;
    private readonly DocumentEmailContextResolver _contextResolver;
    private readonly IRepository<AppointmentDocument, Guid> _documentRepository;
    private readonly ISettingProvider _settingProvider;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<DocumentRejectedEmailHandler> _logger;

    public DocumentRejectedEmailHandler(
        INotificationDispatcher dispatcher,
        DocumentEmailContextResolver contextResolver,
        IRepository<AppointmentDocument, Guid> documentRepository,
        ISettingProvider settingProvider,
        ICurrentTenant currentTenant,
        ILogger<DocumentRejectedEmailHandler> logger)
    {
        _dispatcher = dispatcher;
        _contextResolver = contextResolver;
        _documentRepository = documentRepository;
        _settingProvider = settingProvider;
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

            // Phase 6.B (Adrian Decision 6.1, 2026-05-08): pick template
            // by (IsAdHoc, IsJointDeclaration) -- 3 OLD-parity paths.
            var templateCode = DocumentNotificationContext.ClassifyDocumentTemplateCode(
                DocumentEmailKind.Rejected,
                ctx.IsAdHoc,
                ctx.IsJointDeclaration);

            // Phase 5 (Category 5, 2026-05-10): for package docs, swap to
            // *RemainingDocs variant when the appointment still has Pending
            // package docs awaiting upload.
            var finalVariables = variables;
            if (!ctx.IsAdHoc && !ctx.IsJointDeclaration)
            {
                var remainingCount = await CountRemainingPendingPackageDocsAsync(
                    eventData.AppointmentId, eventData.AppointmentDocumentId);
                if (remainingCount > 0)
                {
                    templateCode = NotificationTemplateConsts.Codes.PatientDocumentRejectedRemainingDocs;
                    finalVariables = await BuildVariablesWithRemainingAsync(variables, eventData.AppointmentId, remainingCount);
                }
            }

            await _dispatcher.DispatchAsync(
                templateCode: templateCode,
                recipients: recipients,
                variables: finalVariables,
                contextTag: $"DocumentRejected/{templateCode}/{eventData.AppointmentDocumentId}");
        }
    }

    private async Task<int> CountRemainingPendingPackageDocsAsync(Guid appointmentId, Guid currentDocumentId)
    {
        var queryable = await _documentRepository.GetQueryableAsync();
        return queryable
            .Where(d => d.AppointmentId == appointmentId &&
                        d.Id != currentDocumentId &&
                        !d.IsAdHoc &&
                        !d.IsJointDeclaration &&
                        d.Status == DocumentStatus.Pending)
            .Count();
    }

    private async Task<IReadOnlyDictionary<string, object?>> BuildVariablesWithRemainingAsync(
        IReadOnlyDictionary<string, object?> baseVariables,
        Guid appointmentId,
        int remainingCount)
    {
        var portalUrl = await _settingProvider.GetOrNullAsync(
            CaseEvaluationSettings.NotificationsPolicy.PortalBaseUrl);
        var url = $"{portalUrl?.TrimEnd('/')}/appointments/view/{appointmentId:N}";
        return new Dictionary<string, object?>(baseVariables, StringComparer.Ordinal)
        {
            ["RemainingDocumentCount"] = remainingCount,
            ["URL"] = url,
        };
    }
}
