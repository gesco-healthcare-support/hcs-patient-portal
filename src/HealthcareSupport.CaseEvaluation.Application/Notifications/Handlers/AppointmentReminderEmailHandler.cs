using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.Data;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace HealthcareSupport.CaseEvaluation.Notifications.Handlers;

/// <summary>
/// Group F (2026-06-09) -- single consolidated reminder. Subscribes to
/// <see cref="AppointmentReminderEto"/> and sends ONE email addressed To the
/// booker with the other parties + the office CC'd
/// (<see cref="BookerCcDispatcher"/>), combining the due-date nudge with the
/// list of any outstanding documents (by label). Replaces the three former
/// handlers (DueDateApproaching, DueDateDocumentIncomplete, PackageDocumentReminder)
/// and their per-recipient / per-document fan-out, eliminating the
/// multi-email-per-day redundancy.
///
/// <para>Outstanding documents = the active package's required documents not
/// yet Accepted (<see cref="MissingRequiredDocumentsResolver"/>), limited to the
/// ones still needing the booker's action -- NotUploaded or Rejected.
/// AwaitingReview docs (uploaded, pending staff review) are NOT listed: there is
/// nothing for the booker to upload. The Joint Declaration Form is tracked
/// outside the package model, so it is folded in separately as a labeled item
/// when this is an AME appointment with no JDF on file.</para>
/// </summary>
public class AppointmentReminderEmailHandler :
    ILocalEventHandler<AppointmentReminderEto>,
    ITransientDependency
{
    private readonly DocumentEmailContextResolver _contextResolver;
    private readonly IAppointmentRecipientResolver _recipientResolver;
    private readonly MissingRequiredDocumentsResolver _missingDocsResolver;
    private readonly BookerCcDispatcher _bookerCcDispatcher;
    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly IRepository<AppointmentDocument, Guid> _documentRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<AppointmentReminderEmailHandler> _logger;

    public AppointmentReminderEmailHandler(
        DocumentEmailContextResolver contextResolver,
        IAppointmentRecipientResolver recipientResolver,
        MissingRequiredDocumentsResolver missingDocsResolver,
        BookerCcDispatcher bookerCcDispatcher,
        IRepository<Appointment, Guid> appointmentRepository,
        IRepository<AppointmentDocument, Guid> documentRepository,
        ICurrentTenant currentTenant,
        ILogger<AppointmentReminderEmailHandler> logger)
    {
        _contextResolver = contextResolver;
        _recipientResolver = recipientResolver;
        _missingDocsResolver = missingDocsResolver;
        _bookerCcDispatcher = bookerCcDispatcher;
        _appointmentRepository = appointmentRepository;
        _documentRepository = documentRepository;
        _currentTenant = currentTenant;
        _logger = logger;
    }

    [UnitOfWork]
    public virtual async Task HandleEventAsync(AppointmentReminderEto eventData)
    {
        if (eventData == null)
        {
            return;
        }

        using (_currentTenant.Change(eventData.TenantId))
        {
            var ctx = await _contextResolver.ResolveAsync(eventData.AppointmentId, appointmentDocumentId: null);
            if (ctx == null)
            {
                _logger.LogWarning(
                    "AppointmentReminderEmailHandler: appointment {AppointmentId} not found; skipping.",
                    eventData.AppointmentId);
                return;
            }

            var resolverOutput = await _recipientResolver.ResolveAsync(
                eventData.AppointmentId,
                NotificationKind.DueDateApproachingReminder);
            var stakeholders = resolverOutput
                .Where(r => !string.IsNullOrWhiteSpace(r.To))
                .Select(r => new NotificationRecipient(
                    email: r.To,
                    role: r.Role,
                    isRegistered: r.IsRegistered))
                .ToList();
            if (stakeholders.Count == 0)
            {
                _logger.LogInformation(
                    "AppointmentReminderEmailHandler: no recipients for appointment {AppointmentId}; skipping.",
                    eventData.AppointmentId);
                return;
            }

            var outstandingDocs = await BuildOutstandingDocsBlockAsync(eventData.AppointmentId);

            var variables = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["BookerFullName"] = ResolveGreetingName(ctx),
                ["AppointmentRequestConfirmationNumber"] = ctx.RequestConfirmationNumber,
                ["DueDate"] = ctx.DueDate.HasValue
                    ? ctx.DueDate.Value.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture)
                    : string.Empty,
                ["OutstandingDocuments"] = outstandingDocs,
                ["PortalUrl"] = ctx.PortalBaseUrl ?? string.Empty,
                ["ClinicName"] = _currentTenant.Name ?? string.Empty,
            };

            // Phase 4 (C3/D3): when the booker is a promoted attorney-creator,
            // address To the named attorney and CC the firm/paralegal creator
            // (often not otherwise a resolved party). Scoped to the promoted case
            // so non-attorney bookers are unchanged; BookerCcDispatcher dedups a CC
            // equal to the To.
            var reminderRecipients = stakeholders;
            if (ctx.IsPromoted && !string.IsNullOrWhiteSpace(ctx.CreatorEmail))
            {
                reminderRecipients = new List<NotificationRecipient>(stakeholders)
                {
                    new(email: ctx.CreatorEmail!, role: RecipientRole.OfficeAdmin, isRegistered: true),
                };
            }

            await _bookerCcDispatcher.DispatchToBookerWithCcAsync(
                templateCode: NotificationTemplateConsts.Codes.AppointmentDueDateReminder,
                bookerEmail: ctx.PrimaryRecipientEmail ?? ctx.BookerEmail,
                stakeholders: reminderRecipients,
                variables: variables,
                contextTag: $"AppointmentReminder/T-{eventData.DaysUntilDue}/{eventData.AppointmentId}");
        }
    }

    /// <summary>
    /// Builds the "##OutstandingDocuments##" HTML block: the required documents
    /// still needing the booker's action (NotUploaded or Rejected) plus the JDF
    /// when outstanding. Returns an empty string when nothing is outstanding, so
    /// the email renders as a clean due-date nudge.
    /// </summary>
    private async Task<string> BuildOutstandingDocsBlockAsync(Guid appointmentId)
    {
        var items = new List<string>();

        var missing = await _missingDocsResolver.ResolveAsync(appointmentId);
        foreach (var doc in missing.Missing)
        {
            switch (doc.State)
            {
                case RequiredDocumentState.NotUploaded:
                    items.Add(WebUtility.HtmlEncode(doc.Name));
                    break;
                case RequiredDocumentState.Rejected:
                    items.Add($"{WebUtility.HtmlEncode(doc.Name)} (rejected - please re-upload)");
                    break;
                    // AwaitingReview: uploaded, pending staff review -- no booker action.
            }
        }

        // The JDF is a standalone IsJointDeclaration document (no SourceDocumentId),
        // so it is not in the package model the resolver reads. Fold it in as a
        // labeled item, keeping the auto-cancel warning inline.
        if (await IsJointDeclarationOutstandingAsync(appointmentId))
        {
            items.Add("Joint Declaration Form (required - the appointment is auto-cancelled if not uploaded by the due date)");
        }

        if (items.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        sb.Append("<p><strong>Documents still needed:</strong></p>");
        sb.Append("<ul style=\"margin:8px 0 16px 0;\">");
        foreach (var item in items)
        {
            sb.Append("<li>").Append(item).Append("</li>");
        }
        sb.Append("</ul>");
        sb.Append("<p>Please log in to the appointment portal and upload the outstanding documents before the due date.</p>");
        return sb.ToString();
    }

    /// <summary>
    /// True when this is an AME appointment with no Joint Declaration document on
    /// file in a non-Rejected status -- the same predicate the JDF auto-cancel
    /// job uses (<c>JointDeclarationAutoCancelJob</c>).
    /// </summary>
    private async Task<bool> IsJointDeclarationOutstandingAsync(Guid appointmentId)
    {
        var appointment = await _appointmentRepository.FindAsync(appointmentId);
        if (appointment == null ||
            appointment.AppointmentTypeId != CaseEvaluationSeedIds.AppointmentTypes.Ame)
        {
            return false;
        }

        var documentQueryable = await _documentRepository.GetQueryableAsync();
        var hasJdf = documentQueryable.Any(d =>
            d.AppointmentId == appointmentId &&
            d.IsJointDeclaration &&
            d.Status != DocumentStatus.Rejected);
        return !hasJdf;
    }

    /// <summary>
    /// The name to greet: the promoted attorney (Phase 4, when the booker is an
    /// attorney-creator), else the booker's, falling back to the patient's, then
    /// to a neutral "there" -- so the email never renders "Hello ,".
    /// </summary>
    private static string ResolveGreetingName(DocumentEmailContext ctx)
    {
        if (!string.IsNullOrWhiteSpace(ctx.GreetingName))
        {
            return ctx.GreetingName;
        }
        if (!string.IsNullOrWhiteSpace(ctx.BookerFullName))
        {
            return ctx.BookerFullName;
        }
        var patientName = $"{ctx.PatientFirstName} {ctx.PatientLastName}".Trim();
        return string.IsNullOrWhiteSpace(patientName) ? "there" : patientName;
    }
}
