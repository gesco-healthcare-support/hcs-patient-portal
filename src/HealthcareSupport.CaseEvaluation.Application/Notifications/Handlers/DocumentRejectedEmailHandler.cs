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
    private readonly IAppointmentRecipientResolver _recipientResolver;
    private readonly MissingRequiredDocumentsResolver _missingRequiredDocumentsResolver;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<DocumentRejectedEmailHandler> _logger;
    // BUG-029 v3 fix (2026-05-21).
    private readonly IAccountUrlBuilder _accountUrlBuilder;

    public DocumentRejectedEmailHandler(
        INotificationDispatcher dispatcher,
        DocumentEmailContextResolver contextResolver,
        IAppointmentRecipientResolver recipientResolver,
        MissingRequiredDocumentsResolver missingRequiredDocumentsResolver,
        ICurrentTenant currentTenant,
        ILogger<DocumentRejectedEmailHandler> logger,
        IAccountUrlBuilder accountUrlBuilder)
    {
        _dispatcher = dispatcher;
        _contextResolver = contextResolver;
        _recipientResolver = recipientResolver;
        _missingRequiredDocumentsResolver = missingRequiredDocumentsResolver;
        _currentTenant = currentTenant;
        _logger = logger;
        _accountUrlBuilder = accountUrlBuilder;
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

            // E3 (2026-06-04): single To+CC message -- To the uploader, CC the
            // other appointment parties. REJECTION excludes the office mailbox
            // (office staff performed the reject). The dispatcher dedups the To
            // address out of the CC list.
            var parties = await _recipientResolver.ResolveAsync(
                eventData.AppointmentId, NotificationKind.DocumentRejected);
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
                var missing = await _missingRequiredDocumentsResolver.ResolveAsync(eventData.AppointmentId);
                if (missing.Missing.Count > 0)
                {
                    templateCode = NotificationTemplateConsts.Codes.PatientDocumentRejectedRemainingDocs;
                    finalVariables = await BuildVariablesWithRemainingAsync(
                        variables, eventData.AppointmentId, missing.Missing);
                }
            }

            // E3: shared "log in or register to view" CTA -- tenant login link
            // (register reachable from the login page), consistent with E2.
            var loginUrl = await BuildLoginUrlAsync(
                eventData.TenantId, parties.Count > 0 ? parties[0].TenantName : _currentTenant.Name);
            var dispatchVariables = new Dictionary<string, object?>(finalVariables, StringComparer.Ordinal)
            {
                ["LoginUrl"] = loginUrl,
                ["UploaderFullName"] = ctx.UploaderFullName,
                ["DocumentLabel"] = ctx.DocumentLabel,
            };

            await _dispatcher.DispatchToWithCcAsync(
                templateCode: templateCode,
                to: to,
                cc: cc,
                variables: dispatchVariables,
                contextTag: $"DocumentRejected/{templateCode}/{eventData.AppointmentDocumentId}");
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

    private async Task<IReadOnlyDictionary<string, object?>> BuildVariablesWithRemainingAsync(
        IReadOnlyDictionary<string, object?> baseVariables,
        Guid appointmentId,
        IReadOnlyList<MissingRequiredDocument> missing)
    {
        // BUG-029 v3 fix (2026-05-21).
        var portalUrl = await _accountUrlBuilder.BuildPortalRootUrlAsync(_currentTenant.Id);
        var url = $"{portalUrl.TrimEnd('/')}/appointments/view/{appointmentId:N}";
        // 2026-06-09: list the outstanding required documents by name.
        var listHtml = string.Concat(missing.Select(m => $"<li>{m.Name}</li>"));
        return new Dictionary<string, object?>(baseVariables, StringComparer.Ordinal)
        {
            ["RemainingDocumentCount"] = missing.Count,
            ["RemainingDocumentList"] = listHtml,
            ["URL"] = url,
        };
    }
}
