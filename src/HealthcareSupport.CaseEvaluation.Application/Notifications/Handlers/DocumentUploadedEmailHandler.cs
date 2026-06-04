using System;
using System.Collections.Generic;
using System.Linq;
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
    private readonly IAppointmentRecipientResolver _recipientResolver;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<DocumentUploadedEmailHandler> _logger;
    // E3 (2026-06-04): tenant-aware login link for the shared To+CC body.
    private readonly IAccountUrlBuilder _accountUrlBuilder;

    public DocumentUploadedEmailHandler(
        INotificationDispatcher dispatcher,
        DocumentEmailContextResolver contextResolver,
        IAppointmentRecipientResolver recipientResolver,
        ICurrentTenant currentTenant,
        ILogger<DocumentUploadedEmailHandler> logger,
        IAccountUrlBuilder accountUrlBuilder)
    {
        _dispatcher = dispatcher;
        _contextResolver = contextResolver;
        _recipientResolver = recipientResolver;
        _currentTenant = currentTenant;
        _logger = logger;
        _accountUrlBuilder = accountUrlBuilder;
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

            if (string.IsNullOrWhiteSpace(uploaderEmail))
            {
                _logger.LogInformation(
                    "DocumentUploadedEmailHandler: no uploader email resolved for document {DocumentId} on appointment {AppointmentId}; skipping.",
                    eventData.AppointmentDocumentId,
                    eventData.AppointmentId);
                return;
            }

            // E3 (2026-06-04): single To+CC message -- To the uploader, CC every
            // other appointment party. UPLOAD keeps the office mailbox in the CC
            // (staff want a copy of new uploads). The dispatcher dedups the To
            // address out of the CC list.
            var parties = await _recipientResolver.ResolveAsync(
                eventData.AppointmentId, NotificationKind.DocumentUploaded);
            var to = new NotificationRecipient(
                email: uploaderEmail!,
                role: RecipientRole.Patient,
                isRegistered: eventData.UploadedByUserId.HasValue);
            var cc = parties
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
                DocumentEmailKind.Uploaded,
                ctx.IsAdHoc,
                ctx.IsJointDeclaration);

            // E3: shared "log in or register to view" CTA -- tenant login link
            // (register reachable from the login page), consistent with E2.
            var loginUrl = await BuildLoginUrlAsync(
                eventData.TenantId, parties.Count > 0 ? parties[0].TenantName : _currentTenant.Name);
            var dispatchVariables = new Dictionary<string, object?>(variables, StringComparer.Ordinal)
            {
                ["LoginUrl"] = loginUrl,
            };

            await _dispatcher.DispatchToWithCcAsync(
                templateCode: templateCode,
                to: to,
                cc: cc,
                variables: dispatchVariables,
                contextTag: $"DocumentUploaded/{templateCode}/{eventData.AppointmentDocumentId}");
        }
    }

    /// <summary>
    /// E3 (2026-06-04): tenant login link for the shared document body. Reuses
    /// E1's login-URL composer for a link shape identical to the E2 booking
    /// email; no per-recipient email pre-fill (the body is CC'd to many).
    /// <paramref name="tenantName"/> only adds the optional <c>?__tenant=</c>
    /// hint -- the tenant is already in the auth-server subdomain.
    /// </summary>
    private async Task<string> BuildLoginUrlAsync(Guid? tenantId, string? tenantName)
    {
        var authServerBaseUrl = await _accountUrlBuilder.BuildAuthServerRootUrlAsync(tenantId);
        return BookingSubmissionEmailHandler.BuildLoginUrl(authServerBaseUrl, tenantName, string.Empty);
    }
}
