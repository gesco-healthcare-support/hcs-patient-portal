using System;
using System.Text;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments.Jobs;
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
/// W1-1f-A-cleanup (Cap B). Subscribes to <see cref="AppointmentSubmittedEto"/>
/// and dispatches two emails on every successful submission:
///   1. Office "new appointment request" -- recipient pulled from the
///      tenant's <c>CaseEvaluation.Notifications.OfficeEmail</c> ABP setting.
///      If unset (default), the office email is skipped silently.
///   2. Booker "request received" confirmation -- recipient is the booker's
///      IdentityUser email.
///
/// Inline HTML body strings (matches W1-2 StatusChangeEmailHandler pattern).
/// SMTP failures swallowed with a warning so the user-visible submission
/// still completes (placeholder ACS creds in dev throw at the transport
/// layer).
/// </summary>
public class SubmissionEmailHandler :
    ILocalEventHandler<AppointmentSubmittedEto>,
    ITransientDependency
{
    private readonly IRepository<Patient, Guid> _patientRepository;
    private readonly IRepository<IdentityUser, Guid> _identityUserRepository;
    private readonly ISettingProvider _settingProvider;
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly ILogger<SubmissionEmailHandler> _logger;

    public SubmissionEmailHandler(
        IRepository<Patient, Guid> patientRepository,
        IRepository<IdentityUser, Guid> identityUserRepository,
        ISettingProvider settingProvider,
        IBackgroundJobManager backgroundJobManager,
        ILogger<SubmissionEmailHandler> logger)
    {
        _patientRepository = patientRepository;
        _identityUserRepository = identityUserRepository;
        _settingProvider = settingProvider;
        _backgroundJobManager = backgroundJobManager;
        _logger = logger;
    }

    [UnitOfWork]
    public virtual async Task HandleEventAsync(AppointmentSubmittedEto eventData)
    {
        var bookerUser = await _identityUserRepository.FindAsync(eventData.BookerUserId);
        var patient = await _patientRepository.FindAsync(eventData.PatientId);
        var bookerName = ResolveBookerName(bookerUser, patient);
        var patientName = ResolvePatientName(patient);
        var dateLine = eventData.AppointmentDate.ToString("MMM d, yyyy h:mm tt");

        await SendOfficeEmailAsync(eventData, bookerName, patientName, dateLine);
        await SendBookerConfirmationAsync(eventData, bookerUser, patient, dateLine);
    }

    private async Task SendOfficeEmailAsync(
        AppointmentSubmittedEto eventData,
        string bookerName,
        string patientName,
        string dateLine)
    {
        var officeEmail = await _settingProvider.GetOrNullAsync(CaseEvaluationSettings.NotificationsPolicy.OfficeEmail);
        if (string.IsNullOrWhiteSpace(officeEmail))
        {
            _logger.LogInformation(
                "SubmissionEmailHandler: office email not configured (CaseEvaluation.Notifications.OfficeEmail empty); skipping office notification for appointment {AppointmentId}.",
                eventData.AppointmentId);
            return;
        }

        var subject = $"New appointment request {eventData.RequestConfirmationNumber}";
        var body = BuildHtml(
            title: "A new appointment request was submitted",
            intro: $"Confirmation #{eventData.RequestConfirmationNumber} requested for {dateLine}.",
            details: $"<p><strong>Booker:</strong> {WebEncode(bookerName)}<br><strong>Patient:</strong> {WebEncode(patientName)}</p><p>Open the appointments queue in the patient portal to review and approve, reject, or send the request back for more info.</p>");

        await _backgroundJobManager.EnqueueAsync(new SendAppointmentEmailArgs
        {
            To = officeEmail,
            Subject = subject,
            Body = body,
            IsBodyHtml = true,
            Context = $"Submission/Office/{eventData.AppointmentId}",
        });
    }

    private async Task SendBookerConfirmationAsync(
        AppointmentSubmittedEto eventData,
        IdentityUser? bookerUser,
        Patient? patient,
        string dateLine)
    {
        var bookerEmail = !string.IsNullOrWhiteSpace(bookerUser?.Email)
            ? bookerUser.Email
            : patient?.Email;
        if (string.IsNullOrWhiteSpace(bookerEmail))
        {
            _logger.LogWarning(
                "SubmissionEmailHandler: no booker email for appointment {AppointmentId}; skipping confirmation receipt.",
                eventData.AppointmentId);
            return;
        }

        var subject = $"Appointment request received - {eventData.RequestConfirmationNumber}";
        var body = BuildHtml(
            title: "We received your appointment request",
            intro: $"Your request for {dateLine} has been submitted (Confirmation #{eventData.RequestConfirmationNumber}).",
            details: "<p>The office will review your request and get back to you. If they need anything further they will send the request back with the fields they would like you to update; you can edit and resubmit through the patient portal.</p><p>You will receive a separate email when the office approves or rejects your request.</p>");

        await _backgroundJobManager.EnqueueAsync(new SendAppointmentEmailArgs
        {
            To = bookerEmail,
            Subject = subject,
            Body = body,
            IsBodyHtml = true,
            Context = $"Submission/Booker/{eventData.AppointmentId}",
        });
    }

    private static string ResolveBookerName(IdentityUser? bookerUser, Patient? patient)
    {
        if (bookerUser != null)
        {
            var name = $"{bookerUser.Name} {bookerUser.Surname}".Trim();
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }
            if (!string.IsNullOrWhiteSpace(bookerUser.Email))
            {
                return bookerUser.Email;
            }
        }
        if (patient != null)
        {
            var name = $"{patient.FirstName} {patient.LastName}".Trim();
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }
        }
        return "(unknown booker)";
    }

    private static string ResolvePatientName(Patient? patient)
    {
        if (patient == null)
        {
            return "(unknown patient)";
        }
        var name = $"{patient.FirstName} {patient.LastName}".Trim();
        return string.IsNullOrWhiteSpace(name) ? "(unnamed patient)" : name;
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
