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
    private readonly INotificationDispatcher _dispatcher;
    private readonly DocumentEmailContextResolver _contextResolver;
    private readonly IAppointmentRecipientResolver _recipientResolver;
    private readonly IRepository<AppointmentDocument, Guid> _documentRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<DocumentAcceptedEmailHandler> _logger;
    // BUG-029 v3 fix (2026-05-21): tenant-aware URL composition.
    private readonly IAccountUrlBuilder _accountUrlBuilder;

    public DocumentAcceptedEmailHandler(
        INotificationDispatcher dispatcher,
        DocumentEmailContextResolver contextResolver,
        IAppointmentRecipientResolver recipientResolver,
        IRepository<AppointmentDocument, Guid> documentRepository,
        ICurrentTenant currentTenant,
        ILogger<DocumentAcceptedEmailHandler> logger,
        IAccountUrlBuilder accountUrlBuilder)
    {
        _dispatcher = dispatcher;
        _contextResolver = contextResolver;
        _recipientResolver = recipientResolver;
        _documentRepository = documentRepository;
        _currentTenant = currentTenant;
        _logger = logger;
        _accountUrlBuilder = accountUrlBuilder;
    }

    [UnitOfWork]
    public virtual async Task HandleEventAsync(AppointmentDocumentAcceptedEto eventData)
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
                    "DocumentAcceptedEmailHandler: appointment {AppointmentId} not found; skipping.",
                    eventData.AppointmentId);
                return;
            }

            var uploaderEmail = await _contextResolver.ResolveUploaderEmailAsync(
                ctx.DocumentUploadedByUserId,
                ctx.PatientEmail ?? ctx.BookerEmail);

            if (string.IsNullOrWhiteSpace(uploaderEmail))
            {
                _logger.LogInformation(
                    "DocumentAcceptedEmailHandler: no uploader email resolved for document {DocumentId} on appointment {AppointmentId}; skipping.",
                    eventData.AppointmentDocumentId,
                    eventData.AppointmentId);
                return;
            }

            // E3 (2026-06-04): single To+CC message -- To the uploader, CC the
            // other appointment parties. APPROVAL excludes the office mailbox
            // (office staff performed the approve). The dispatcher dedups the
            // To address out of the CC list.
            var parties = await _recipientResolver.ResolveAsync(
                eventData.AppointmentId, NotificationKind.DocumentAccepted);
            var to = new NotificationRecipient(
                email: uploaderEmail!,
                role: RecipientRole.Patient,
                isRegistered: ctx.DocumentUploadedByUserId.HasValue);
            var cc = parties
                .Where(p => p.Role != RecipientRole.OfficeAdmin)
                .Select(p => new NotificationRecipient(
                    email: p.To,
                    role: p.Role ?? RecipientRole.Patient,
                    isRegistered: p.IsRegistered))
                .ToList();

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
                DocumentEmailKind.Accepted,
                ctx.IsAdHoc,
                ctx.IsJointDeclaration);

            // Phase 5 (Category 5, 2026-05-10): for package docs, swap to
            // *RemainingDocs variant when the appointment still has Pending
            // package docs awaiting upload. Ad-hoc + JDF flows do not get
            // this branch (no queue semantics).
            var finalVariables = variables;
            if (!ctx.IsAdHoc && !ctx.IsJointDeclaration)
            {
                var remainingCount = await CountRemainingPendingPackageDocsAsync(
                    eventData.AppointmentId, eventData.AppointmentDocumentId);
                if (remainingCount > 0)
                {
                    templateCode = NotificationTemplateConsts.Codes.PatientDocumentAcceptedRemainingDocs;
                    finalVariables = await BuildVariablesWithRemainingAsync(variables, eventData.AppointmentId, remainingCount);
                }
            }

            // E3: shared "log in or register to view" CTA -- tenant login link
            // (register reachable from the login page), consistent with E2.
            var loginUrl = await BuildLoginUrlAsync(
                eventData.TenantId, parties.Count > 0 ? parties[0].TenantName : _currentTenant.Name);
            var dispatchVariables = new Dictionary<string, object?>(finalVariables, StringComparer.Ordinal)
            {
                ["LoginUrl"] = loginUrl,
            };

            await _dispatcher.DispatchToWithCcAsync(
                templateCode: templateCode,
                to: to,
                cc: cc,
                variables: dispatchVariables,
                contextTag: $"DocumentAccepted/{templateCode}/{eventData.AppointmentDocumentId}");
        }
    }

    /// <summary>
    /// E3 (2026-06-04): tenant login link for the shared document body. Reuses
    /// E1's login-URL composer for a link shape identical to the E2 booking
    /// email; no per-recipient email pre-fill (the body is CC'd to many).
    /// </summary>
    private async Task<string> BuildLoginUrlAsync(Guid? tenantId, string? tenantName)
    {
        var authServerBaseUrl = await _accountUrlBuilder.BuildAuthServerRootUrlAsync(tenantId);
        return BookingSubmissionEmailHandler.BuildLoginUrl(authServerBaseUrl, tenantName, string.Empty);
    }

    /// <summary>
    /// Counts Pending package docs (non-ad-hoc, non-JDF) for the appointment,
    /// excluding the document that just transitioned to Accepted. The event
    /// fires after the status flip, so the current doc may or may not still
    /// appear as Pending depending on save ordering; the Id-exclude is the
    /// safest guard either way.
    /// </summary>
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
        // BUG-029 v3 fix (2026-05-21): tenant-aware portal URL via builder.
        var portalUrl = await _accountUrlBuilder.BuildPortalRootUrlAsync(_currentTenant.Id);
        var url = $"{portalUrl.TrimEnd('/')}/appointments/view/{appointmentId:N}";
        return new Dictionary<string, object?>(baseVariables, StringComparer.Ordinal)
        {
            ["RemainingDocumentCount"] = remainingCount,
            ["URL"] = url,
        };
    }
}
