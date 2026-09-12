using System.Text;
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
/// Group K (G-02-03, 2026-06-06) -- subscribes to
/// <see cref="AppointmentIntakeChangedEto"/> and emails the appointment
/// stakeholders an OLD-parity per-field diff table via the
/// <c>AppointmentChangeLogs</c> template. When the date/time changed it also
/// dispatches the one-shot <c>AppointmentRescheduleRequestByAdmin</c> email.
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
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<IntakeChangedEmailHandler> _logger;

    public IntakeChangedEmailHandler(
        INotificationDispatcher dispatcher,
        DocumentEmailContextResolver contextResolver,
        IAppointmentRecipientResolver recipientResolver,
        ICurrentTenant currentTenant,
        ILogger<IntakeChangedEmailHandler> logger)
    {
        _dispatcher = dispatcher;
        _contextResolver = contextResolver;
        _recipientResolver = recipientResolver;
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

            var resolverOutput = await _recipientResolver.ResolveAsync(
                eventData.AppointmentId,
                NotificationKind.IntakeChanged);

            var recipients = resolverOutput
                .Where(r => !string.IsNullOrWhiteSpace(r.To))
                .Select(r => new NotificationRecipient(
                    email: r.To,
                    role: r.Role,
                    isRegistered: r.IsRegistered))
                .ToList();

            if (recipients.Count == 0)
            {
                _logger.LogInformation(
                    "IntakeChangedEmailHandler: no recipients resolved for appointment {AppointmentId}; skipping.",
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
                clinicName: _currentTenant.Name,
                portalUrl: ctx.PortalBaseUrl);

            var diffVariables = new Dictionary<string, object?>(baseVariables, StringComparer.Ordinal)
            {
                ["AppointmentChangeLogs"] = BuildDiffTable(eventData.ChangedFields),
            };

            await _dispatcher.DispatchAsync(
                templateCode: NotificationTemplateConsts.Codes.AppointmentChangeLogs,
                recipients: recipients,
                variables: diffVariables,
                contextTag: $"IntakeChanged/{eventData.AppointmentId}");

            if (eventData.DateOrTimeChanged)
            {
                var rescheduleVariables = new Dictionary<string, object?>(baseVariables, StringComparer.Ordinal)
                {
                    // ctx.AppointmentDate is the post-update date/time.
                    ["NewAppointmentDate"] = baseVariables.TryGetValue("AppointmentDate", out var date)
                        ? date
                        : string.Empty,
                };

                await _dispatcher.DispatchAsync(
                    templateCode: NotificationTemplateConsts.Codes.AppointmentRescheduleRequestByAdmin,
                    recipients: recipients,
                    variables: rescheduleVariables,
                    contextTag: $"IntakeChanged/Reschedule/{eventData.AppointmentId}");
            }
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
