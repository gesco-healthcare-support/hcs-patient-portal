using System;
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
/// Transition email handler. Subscribes to
/// <see cref="AppointmentStatusChangedEto"/> and dispatches a status-specific
/// email to the appointment's IdentityUser (the booker) via ABP's
/// <see cref="IEmailSender"/>. Per OLD spec (Phase 0.2, 2026-05-01) only the
/// Approved and Rejected transitions email here; the AwaitingMoreInfo /
/// SendBack flow has been removed -- corrections happen via reject + re-request.
/// Inline body strings at MVP -- defer ABP TextTemplating to post-MVP cleanup.
/// </summary>
public class StatusChangeEmailHandler :
    ILocalEventHandler<AppointmentStatusChangedEto>,
    ITransientDependency
{
    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly IRepository<Patient, Guid> _patientRepository;
    private readonly IRepository<IdentityUser, Guid> _identityUserRepository;
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly ISettingProvider _settingProvider;
    private readonly IAppointmentRecipientResolver _recipientResolver;
    private readonly ILogger<StatusChangeEmailHandler> _logger;

    public StatusChangeEmailHandler(
        IRepository<Appointment, Guid> appointmentRepository,
        IRepository<Patient, Guid> patientRepository,
        IRepository<IdentityUser, Guid> identityUserRepository,
        IBackgroundJobManager backgroundJobManager,
        ISettingProvider settingProvider,
        IAppointmentRecipientResolver recipientResolver,
        ILogger<StatusChangeEmailHandler> logger)
    {
        _appointmentRepository = appointmentRepository;
        _patientRepository = patientRepository;
        _identityUserRepository = identityUserRepository;
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

        var (subject, body) = BuildEmail(template.Value, appointment, eventData);

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
        _ => NotificationKind.Approved,
    };

    private enum EmailTemplate { Approved, Rejected }

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
            _ => null,
        };
    }

    private static (string Subject, string Body) BuildEmail(
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
            default:
                throw new ArgumentOutOfRangeException(nameof(template), template, null);
        }
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
