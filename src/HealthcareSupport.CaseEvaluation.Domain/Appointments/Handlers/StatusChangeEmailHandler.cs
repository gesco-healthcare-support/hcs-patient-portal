using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments.Jobs;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Patients;
using HealthcareSupport.CaseEvaluation.Settings;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus;
using Volo.Abp.Identity;
using Volo.Abp.Settings;
using Volo.Abp.Uow;

namespace HealthcareSupport.CaseEvaluation.Appointments.Handlers;

/// <summary>
/// W1-2 transition email handler. Subscribes to
/// <see cref="AppointmentStatusChangedEto"/> and dispatches a status-specific
/// email to the appointment's IdentityUser (the booker) via ABP's
/// <see cref="IEmailSender"/>. Inline body strings at MVP -- defer ABP
/// TextTemplating to post-MVP cleanup so we get localizable, admin-editable
/// templates with Razor partials. Logged in
/// docs/plans/deferred-from-mvp.md.
///
/// Recipients at MVP: just the booker's IdentityUser email. The all-parties
/// fan-out (patient cc, attorney cc, doctor's office, claim examiner, etc.)
/// is also deferred (already on ledger from W1 plan).
///
/// Only fires for the 3 transitions that change the booker's status visibly:
/// Approved, Rejected, AwaitingMoreInfo. Pending-from-AwaitingMoreInfo (the
/// booker's resubmit) does NOT email -- the office sees the queue update.
/// </summary>
public class StatusChangeEmailHandler :
    ILocalEventHandler<AppointmentStatusChangedEto>,
    ITransientDependency
{
    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly IRepository<Patient, Guid> _patientRepository;
    private readonly IRepository<IdentityUser, Guid> _identityUserRepository;
    private readonly IRepository<AppointmentSendBackInfo, Guid> _sendBackInfoRepository;
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly ISettingProvider _settingProvider;
    private readonly IAppointmentRecipientResolver _recipientResolver;
    private readonly ILogger<StatusChangeEmailHandler> _logger;

    public StatusChangeEmailHandler(
        IRepository<Appointment, Guid> appointmentRepository,
        IRepository<Patient, Guid> patientRepository,
        IRepository<IdentityUser, Guid> identityUserRepository,
        IRepository<AppointmentSendBackInfo, Guid> sendBackInfoRepository,
        IBackgroundJobManager backgroundJobManager,
        ISettingProvider settingProvider,
        IAppointmentRecipientResolver recipientResolver,
        ILogger<StatusChangeEmailHandler> logger)
    {
        _appointmentRepository = appointmentRepository;
        _patientRepository = patientRepository;
        _identityUserRepository = identityUserRepository;
        _sendBackInfoRepository = sendBackInfoRepository;
        _backgroundJobManager = backgroundJobManager;
        _settingProvider = settingProvider;
        _recipientResolver = recipientResolver;
        _logger = logger;
    }

    [UnitOfWork]
    public virtual async Task HandleEventAsync(AppointmentStatusChangedEto eventData)
    {
        var template = ResolveTemplate(eventData.FromStatus, eventData.ToStatus);
        if (template == null)
        {
            return;
        }

        var appointment = await _appointmentRepository.FindAsync(eventData.AppointmentId);
        if (appointment == null)
        {
            return;
        }

        var (subject, body) = await BuildEmailAsync(template.Value, appointment, eventData);

        // W2-10: fan out to all parties via the shared recipient resolver.
        // MVP renders one body per NotificationKind regardless of role; the
        // resolver tags each arg with a RecipientRole for forward-compat.
        var kind = MapTemplateToKind(template.Value);
        var recipients = await _recipientResolver.ResolveAsync(appointment.Id, kind);
        if (recipients.Count == 0)
        {
            _logger.LogWarning(
                "StatusChangeEmailHandler: resolver returned 0 recipients for appointment {AppointmentId}; skipping {Template}.",
                appointment.Id,
                template);
            return;
        }

        foreach (var args in recipients)
        {
            args.Subject = subject;
            args.Body = body;
            args.IsBodyHtml = true;
            args.Context = $"Transition/{template}/{args.Role}/{appointment.Id}";
            await _backgroundJobManager.EnqueueAsync(args);
        }
    }

    private static NotificationKind MapTemplateToKind(EmailTemplate t) => t switch
    {
        EmailTemplate.Approved => NotificationKind.Approved,
        EmailTemplate.Rejected => NotificationKind.Rejected,
        EmailTemplate.SendBack => NotificationKind.AwaitingMoreInfo,
        _ => NotificationKind.Approved,
    };

    private enum EmailTemplate { Approved, Rejected, SendBack }

    // W2-3: from/to are now nullable on the ETO so initial-create + delete
    // events flow through the same handler chain. Non-transition events
    // (no ToStatus, or a ToStatus not in the transition templates) just
    // return null which short-circuits the email send.
    private static EmailTemplate? ResolveTemplate(AppointmentStatusType? from, AppointmentStatusType? to)
    {
        if (!to.HasValue)
        {
            return null;
        }
        return to.Value switch
        {
            AppointmentStatusType.Approved => EmailTemplate.Approved,
            AppointmentStatusType.Rejected => EmailTemplate.Rejected,
            AppointmentStatusType.AwaitingMoreInfo => EmailTemplate.SendBack,
            _ => null,
        };
    }

    private async Task<string?> ResolveRecipientEmailAsync(Appointment appointment)
    {
        var bookerUser = await _identityUserRepository.FindAsync(appointment.IdentityUserId);
        if (bookerUser != null && !string.IsNullOrWhiteSpace(bookerUser.Email))
        {
            return bookerUser.Email;
        }

        // Fallback: try the patient's email if the booker user has none on file.
        var patient = await _patientRepository.FindAsync(appointment.PatientId);
        return patient?.Email;
    }

    private async Task<(string Subject, string Body)> BuildEmailAsync(
        EmailTemplate template,
        Appointment appointment,
        AppointmentStatusChangedEto eventData)
    {
        var confirmation = appointment.RequestConfirmationNumber;
        var date = appointment.AppointmentDate.ToString("MMM d, yyyy h:mm tt");

        switch (template)
        {
            case EmailTemplate.Approved:
                return (
                    $"Appointment {confirmation} approved",
                    BuildHtml(
                        title: "Your appointment has been approved",
                        intro: $"Confirmation #{confirmation} scheduled for {date} has been approved by the office.",
                        details: "You will receive separate confirmations from each attending party. Please arrive 15 minutes early."));
            case EmailTemplate.Rejected:
                var reason = string.IsNullOrWhiteSpace(eventData.Reason)
                    ? "No reason provided."
                    : eventData.Reason;
                return (
                    $"Appointment {confirmation} rejected",
                    BuildHtml(
                        title: "Your appointment has been rejected",
                        intro: $"Confirmation #{confirmation} (requested for {date}) was rejected by the office.",
                        details: $"Reason from the office: {WebEncode(reason)}"));
            case EmailTemplate.SendBack:
                var sendBack = await GetLatestSendBackInfoAsync(appointment.Id);
                var note = string.IsNullOrWhiteSpace(sendBack?.Note)
                    ? "No note attached."
                    : sendBack!.Note!;
                var fieldsLine = sendBack?.GetFlaggedFields().Count > 0
                    ? $"Please revisit the flagged fields highlighted on your appointment page: {string.Join(", ", sendBack.GetFlaggedFields())}."
                    : "Please review and resubmit your request.";
                var deepLink = await BuildAppointmentViewLinkAsync(appointment.Id);
                return (
                    $"Appointment {confirmation} needs more information",
                    BuildHtml(
                        title: "The office requested changes",
                        intro: $"Confirmation #{confirmation} (requested for {date}) was sent back for more info.",
                        details: $"<p><strong>Office's note:</strong> {WebEncode(note)}</p><p>{fieldsLine}</p><p><a href=\"{deepLink}\">Open the appointment in the patient portal</a> to make changes and resubmit.</p>"));
            default:
                throw new ArgumentOutOfRangeException(nameof(template), template, null);
        }
    }

    private async Task<AppointmentSendBackInfo?> GetLatestSendBackInfoAsync(Guid appointmentId)
    {
        var queryable = await _sendBackInfoRepository.GetQueryableAsync();
        return queryable
            .Where(x => x.AppointmentId == appointmentId)
            .OrderByDescending(x => x.SentBackAt)
            .FirstOrDefault();
    }

    /// <summary>
    /// Builds an absolute deep-link to the appointment view page, sourcing the
    /// portal base URL from the per-tenant <c>CaseEvaluation.Notifications.PortalBaseUrl</c>
    /// setting (defaults to <c>http://localhost:4200</c> for dev). Trailing
    /// slashes on the base URL are stripped so the joined path stays clean.
    /// </summary>
    private async Task<string> BuildAppointmentViewLinkAsync(Guid appointmentId)
    {
        var configured = await _settingProvider.GetOrNullAsync(CaseEvaluationSettings.NotificationsPolicy.PortalBaseUrl);
        var baseUrl = string.IsNullOrWhiteSpace(configured) ? "http://localhost:4200" : configured!.TrimEnd('/');
        return $"{baseUrl}/appointments/view/{appointmentId}";
    }

    private static string BuildHtml(string title, string intro, string details)
    {
        var sb = new StringBuilder();
        sb.Append("<html><body style=\"font-family: Arial, sans-serif; color: #333;\">");
        sb.Append($"<h2 style=\"color: #0d6efd;\">{WebEncode(title)}</h2>");
        sb.Append($"<p>{WebEncode(intro)}</p>");
        sb.Append($"<div>{details}</div>");
        sb.Append("<hr><p style=\"color: #888; font-size: 0.85em;\">This is an automated notification from the Patient Portal.</p>");
        sb.Append("</body></html>");
        return sb.ToString();
    }

    private static string WebEncode(string raw) => System.Net.WebUtility.HtmlEncode(raw);
}
