using System.Globalization;
using System.Text;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace HealthcareSupport.CaseEvaluation.Notifications.Handlers;

/// <summary>
/// Group K (G-02-03, 2026-06-06) -- subscribes to
/// <see cref="AppointmentIntakeChangedEto"/>, raised when staff save Edit details.
///
/// <para>2026-11-03 (office email routing): a save that MOVES THE DATE is a finished change, so it sends
/// ONE <c>AppointmentRescheduleRequestByAdmin</c> email to the parties, the office inbox and the active
/// Intake Staff (the patient only once the appointment is approved, or when the patient submitted it). A
/// save that moves the date AND edits the panel number or due date is still that one email: the panel
/// number and due date are not shown to the parties, and the office inbox gets no separate change log.
/// A panel-number-only or due-date-only save is the office's own housekeeping: the
/// <c>AppointmentChangeLogs</c> diff table goes to the office inbox only.</para>
///
/// The field values arrive ALREADY PHI-redacted in the ETO (masked at the
/// AppService boundary), so this handler only renders + dispatches.
/// </summary>
public class IntakeChangedEmailHandler :
    ILocalEventHandler<AppointmentIntakeChangedEto>,
    ITransientDependency
{
    private readonly INotificationDispatcher _dispatcher;
    private readonly DocumentEmailContextResolver _contextResolver;
    private readonly IAppointmentRecipientResolver _recipientResolver;
    private readonly OfficeNotificationRecipients _officeRecipients;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<IntakeChangedEmailHandler> _logger;

    public IntakeChangedEmailHandler(
        INotificationDispatcher dispatcher,
        DocumentEmailContextResolver contextResolver,
        IAppointmentRecipientResolver recipientResolver,
        OfficeNotificationRecipients officeRecipients,
        ICurrentTenant currentTenant,
        ILogger<IntakeChangedEmailHandler> logger)
    {
        _dispatcher = dispatcher;
        _contextResolver = contextResolver;
        _recipientResolver = recipientResolver;
        _officeRecipients = officeRecipients;
        _currentTenant = currentTenant;
        _logger = logger;
    }

    [UnitOfWork]
    public virtual async Task HandleEventAsync(AppointmentIntakeChangedEto eventData)
    {
        if (eventData == null || eventData.ChangedFields.Count == 0)
        {
            return;
        }

        using (_currentTenant.Change(eventData.TenantId))
        {
            var ctx = await _contextResolver.ResolveAsync(eventData.AppointmentId, appointmentDocumentId: null);
            if (ctx == null)
            {
                _logger.LogWarning(
                    "IntakeChangedEmailHandler: appointment {AppointmentId} not found; skipping.",
                    eventData.AppointmentId);
                return;
            }

            if (eventData.DateOrTimeChanged)
            {
                await DispatchDateMovedAsync(eventData, ctx);
                return;
            }

            // A panel-number or due-date edit is the office's own housekeeping, so the change log goes
            // to the office inbox ONLY -- no party hears about it.
            var recipients = await _officeRecipients.GetOfficeInboxAsync(
                $"IntakeChanged/{eventData.AppointmentId}");

            if (recipients.Count == 0)
            {
                _logger.LogInformation(
                    "IntakeChangedEmailHandler: no office inbox for appointment {AppointmentId}; skipping.",
                    eventData.AppointmentId);
                return;
            }

            var baseVariables = DocumentNotificationContext.BuildVariables(
                patientFirstName: ctx.PatientFirstName,
                patientLastName: ctx.PatientLastName,
                patientEmail: ctx.PatientEmail,
                requestConfirmationNumber: ctx.RequestConfirmationNumber,
                appointmentDate: ctx.AppointmentDate,
                claimNumber: ctx.ClaimNumber,
                wcabAdj: ctx.WcabAdj,
                documentName: null,
                rejectionNotes: null,
                // Filled by NotificationTemplateRenderer from the tenant store (#1014):
                // ICurrentTenant.Name is null inside Change(TenantId).
                clinicName: null,
                portalUrl: ctx.PortalBaseUrl);

            var diffVariables = new Dictionary<string, object?>(baseVariables, StringComparer.Ordinal)
            {
                ["AppointmentChangeLogs"] = BuildDiffTable(eventData.ChangedFields),
            };

            await _dispatcher.DispatchAsync(
                notificationCase: NotificationCase.Of(eventData.AppointmentId),
                templateCode: NotificationTemplateConsts.Codes.AppointmentChangeLogs,
                recipients: recipients,
                variables: diffVariables,
                contextTag: NotificationOccurrence.At($"IntakeChanged/{eventData.AppointmentId}", eventData.OccurredAt));
        }
    }

    /// <summary>
    /// The one email for a date move: the parties and the office inbox (from the recipient resolver) plus
    /// the active Intake Staff, each address once. Until the appointment is approved the patient is left
    /// out unless the patient submitted it. A switched-off Intake user is never listed.
    /// </summary>
    private async Task DispatchDateMovedAsync(AppointmentIntakeChangedEto eventData, DocumentEmailContext ctx)
    {
        var resolved = await _recipientResolver.ResolveAsync(eventData.AppointmentId, NotificationKind.Approved);
        var recipients = resolved
            .Where(r => !string.IsNullOrWhiteSpace(r.To))
            .Select(r => new NotificationRecipient(email: r.To, role: r.Role, isRegistered: r.IsRegistered))
            .ToList();
        await _officeRecipients.AppendIntakeAsync(recipients);

        // 2026-11-04: the case followers (external booker, accessors) are copied like the parties; a
        // Patient-role accessor follows the patient rule (only once Approved, or if the patient booked).
        recipients = CaseFollowers.Append(
            recipients, ctx, patientRoleAllowed: !PatientBeforeApprovalRule.IsBeforeApproval(ctx.AppointmentStatus) || ctx.PatientIsSubmitter);

        if (PatientBeforeApprovalRule.IsBeforeApproval(ctx.AppointmentStatus))
        {
            recipients = PatientBeforeApprovalRule.Apply(recipients, ctx);
        }

        recipients = OfficeNotificationRecipients.Distinct(recipients);
        if (recipients.Count == 0)
        {
            _logger.LogInformation(
                "IntakeChangedEmailHandler: no recipients for the date move of appointment {AppointmentId}; skipping.",
                eventData.AppointmentId);
            return;
        }

        var variables = new Dictionary<string, object?>(
            DocumentNotificationContext.BuildVariables(
                patientFirstName: ctx.PatientFirstName,
                patientLastName: ctx.PatientLastName,
                patientEmail: ctx.PatientEmail,
                requestConfirmationNumber: ctx.RequestConfirmationNumber,
                appointmentDate: ctx.AppointmentDate,
                claimNumber: ctx.ClaimNumber,
                wcabAdj: ctx.WcabAdj,
                documentName: null,
                rejectionNotes: null,
                // Filled by NotificationTemplateRenderer from the tenant store (#1014).
                clinicName: null,
                portalUrl: ctx.PortalBaseUrl),
            StringComparer.Ordinal)
        {
            ["NewAppointmentDate"] = (eventData.NewAppointmentDate ?? ctx.AppointmentDate)
                .ToString("MM/dd/yyyy h:mm tt", CultureInfo.InvariantCulture),
        };

        // The new slot is in the tag, and so is the time of the save: a second move mails again (even a
        // move back onto an earlier slot), while a replay of the SAME save collapses onto the first send.
        var kind = eventData.NewDoctorAvailabilityId is { } slotId
            ? $"IntakeDateMoved/{eventData.AppointmentId}/{slotId}"
            : $"IntakeDateMoved/{eventData.AppointmentId}";
        try
        {
            await _dispatcher.DispatchAsync(
                notificationCase: NotificationCase.Of(eventData.AppointmentId),
                templateCode: NotificationTemplateConsts.Codes.AppointmentRescheduleRequestByAdmin,
                recipients: recipients,
                variables: variables,
                contextTag: NotificationOccurrence.At(kind, eventData.OccurredAt));
        }
        catch (BusinessException ex)
            when (ex.Code == CaseEvaluationDomainErrorCodes.NotificationTemplateNotFound)
        {
            // This runs inside the staff member's save; a missing or switched-off template must not roll it back.
            _logger.LogError(
                ex,
                "IntakeChangedEmailHandler: template {TemplateCode} missing/inactive; date-move notice skipped for appointment {AppointmentId}.",
                NotificationTemplateConsts.Codes.AppointmentRescheduleRequestByAdmin,
                eventData.AppointmentId);
        }
    }

    private static string BuildDiffTable(IReadOnlyList<IntakeChangedField> fields)
    {
        var sb = new StringBuilder();
        sb.Append("<table border=\"1\" cellpadding=\"6\" cellspacing=\"0\" ")
          .Append("style=\"border-collapse:collapse;width:100%;font-size:13px;\">");
        sb.Append("<tr style=\"background:#f1f1f1;\">")
          .Append("<th align=\"left\">Field</th>")
          .Append("<th align=\"left\">Old value</th>")
          .Append("<th align=\"left\">New value</th></tr>");

        foreach (var field in fields)
        {
            var label = System.Net.WebUtility.HtmlEncode(FriendlyLabel(field.FieldName));
            if (field.ValueRedacted)
            {
                sb.Append($"<tr><td>{label}</td><td colspan=\"2\"><em>updated (value hidden)</em></td></tr>");
            }
            else
            {
                var oldValue = System.Net.WebUtility.HtmlEncode(field.OldValue ?? "-");
                var newValue = System.Net.WebUtility.HtmlEncode(field.NewValue ?? "-");
                sb.Append($"<tr><td>{label}</td><td>{oldValue}</td><td>{newValue}</td></tr>");
            }
        }

        sb.Append("</table>");
        return sb.ToString();
    }

    private static string FriendlyLabel(string fieldName) => fieldName switch
    {
        "AppointmentDate" => "Appointment Date",
        "PanelNumber" => "Panel Number",
        "DueDate" => "Due Date",
        _ => fieldName,
    };
}
