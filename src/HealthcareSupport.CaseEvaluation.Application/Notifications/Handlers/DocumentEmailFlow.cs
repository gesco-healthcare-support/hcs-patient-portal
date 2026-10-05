using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using Microsoft.Extensions.Logging;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.Notifications.Handlers;

/// <summary>
/// What one document email handler decides, handed to <see cref="DocumentEmailFlow"/>. Each
/// handler owns its own event type, its template family (<see cref="EmailKind"/>), its
/// context tag and its recipient policy; everything after that is the same for all three.
/// </summary>
internal sealed class DocumentEmailSpec
{
    /// <summary>Handler name, used only as the prefix of the skip log lines.</summary>
    public required string HandlerName { get; init; }

    /// <summary>Prefix of the dispatcher context tag: <c>{prefix}/{templateCode}/{documentId}</c>.</summary>
    public required string ContextTagPrefix { get; init; }

    public required Guid? TenantId { get; init; }
    public required Guid AppointmentId { get; init; }
    public required Guid? AppointmentDocumentId { get; init; }
    public required NotificationKind Kind { get; init; }
    public required DocumentEmailKind EmailKind { get; init; }

    /// <summary>Whether the office mailbox is dropped from the CC (accept and reject do, upload does not).</summary>
    public required bool ExcludeOfficeFromCc { get; init; }

    /// <summary>Verbatim staff notes for the rejected template; null for the other two.</summary>
    public string? RejectionNotes { get; init; }

    /// <summary>
    /// The uploader to look up, and whether the To counts as a registered user. A delegate over
    /// the resolved context because accept and reject read both from the stored document, while
    /// upload prefers the event's own user id.
    /// </summary>
    public required Func<DocumentEmailContext, (Guid? UserId, bool IsRegistered)> Uploader { get; init; }

    /// <summary>
    /// The template to swap in while required package documents are still outstanding. Null means
    /// this email has no such branch (the upload email).
    /// </summary>
    public string? RemainingDocsTemplateCode { get; init; }
}

/// <summary>
/// The shared body of the document uploaded, accepted and rejected emails: resolve the
/// appointment, find the uploader, build one To+CC message, and dispatch it. Extracted from
/// three handlers that had grown identical recipient, dispatch and logging blocks (#1257).
/// Behaviour is exactly what each handler did before; the handler tests pin it.
/// </summary>
internal sealed class DocumentEmailFlow
{
    private readonly INotificationDispatcher _dispatcher;
    private readonly DocumentEmailContextResolver _contextResolver;
    private readonly IAppointmentRecipientResolver _recipientResolver;
    private readonly MissingRequiredDocumentsResolver? _missingRequiredDocumentsResolver;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger _logger;
    private readonly IAccountUrlBuilder _accountUrlBuilder;

    internal DocumentEmailFlow(
        INotificationDispatcher dispatcher,
        DocumentEmailContextResolver contextResolver,
        IAppointmentRecipientResolver recipientResolver,
        MissingRequiredDocumentsResolver? missingRequiredDocumentsResolver,
        ICurrentTenant currentTenant,
        ILogger logger,
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

    internal async Task SendAsync(DocumentEmailSpec spec)
    {
        using (_currentTenant.Change(spec.TenantId))
        {
            var ctx = await _contextResolver.ResolveAsync(spec.AppointmentId, spec.AppointmentDocumentId);
            if (ctx == null)
            {
                _logger.LogWarning(
                    "{Handler}: appointment {AppointmentId} not found; skipping.",
                    spec.HandlerName,
                    spec.AppointmentId);
                return;
            }

            var uploader = spec.Uploader(ctx);
            var uploaderEmail = await _contextResolver.ResolveUploaderEmailAsync(
                uploader.UserId,
                ctx.PatientEmail ?? ctx.BookerEmail);

            if (string.IsNullOrWhiteSpace(uploaderEmail))
            {
                _logger.LogInformation(
                    "{Handler}: no uploader email resolved for document {DocumentId} on appointment {AppointmentId}; skipping.",
                    spec.HandlerName,
                    spec.AppointmentDocumentId,
                    spec.AppointmentId);
                return;
            }

            // Single To+CC message: To the uploader, CC the other appointment parties.
            // The dispatcher dedups the To address out of the CC list.
            var parties = await _recipientResolver.ResolveAsync(spec.AppointmentId, spec.Kind);
            var to = new NotificationRecipient(
                email: uploaderEmail,
                role: RecipientRole.Patient,
                isRegistered: uploader.IsRegistered);
            var cc = parties
                .Where(p => !spec.ExcludeOfficeFromCc || p.Role != RecipientRole.OfficeAdmin)
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
                rejectionNotes: spec.RejectionNotes,
                // Filled by NotificationTemplateRenderer from the tenant store (#1014):
                // ICurrentTenant.Name is null inside Change(TenantId).
                clinicName: null,
                portalUrl: ctx.PortalBaseUrl);

            // Pick the template by (IsAdHoc, IsJointDeclaration) -- 3 OLD-parity paths.
            var templateCode = DocumentNotificationContext.ClassifyDocumentTemplateCode(
                spec.EmailKind,
                ctx.IsAdHoc,
                ctx.IsJointDeclaration);

            // For package docs, swap to the *RemainingDocs variant while the appointment
            // still has Pending package docs. Ad-hoc and JDF flows have no queue semantics.
            var finalVariables = variables;
            if (spec.RemainingDocsTemplateCode != null && !ctx.IsAdHoc && !ctx.IsJointDeclaration)
            {
                var missing = await _missingRequiredDocumentsResolver!.ResolveAsync(spec.AppointmentId);
                if (missing.Missing.Count > 0)
                {
                    templateCode = spec.RemainingDocsTemplateCode;
                    finalVariables = await DocumentEmailLinkAndListBuilder.BuildVariablesWithRemainingAsync(
                        _accountUrlBuilder, _currentTenant.Id, variables, spec.AppointmentId, missing.Missing);
                }
            }

            // Shared "log in or register to view" CTA -- tenant login link.
            var loginUrl = await DocumentEmailLinkAndListBuilder.BuildLoginUrlAsync(
                _accountUrlBuilder, spec.TenantId, parties.Count > 0 ? parties[0].TenantName : _currentTenant.Name);
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
                contextTag: $"{spec.ContextTagPrefix}/{templateCode}/{spec.AppointmentDocumentId}");
        }
    }
}
