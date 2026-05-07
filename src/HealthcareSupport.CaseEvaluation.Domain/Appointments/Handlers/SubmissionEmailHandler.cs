using System;
using System.Text;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments.Jobs;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
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
    private readonly IAppointmentRecipientResolver _recipientResolver;
    private readonly ILogger<SubmissionEmailHandler> _logger;

    public SubmissionEmailHandler(
        IRepository<Patient, Guid> patientRepository,
        IRepository<IdentityUser, Guid> identityUserRepository,
        ISettingProvider settingProvider,
        IBackgroundJobManager backgroundJobManager,
        IAppointmentRecipientResolver recipientResolver,
        ILogger<SubmissionEmailHandler> logger)
    {
        _patientRepository = patientRepository;
        _identityUserRepository = identityUserRepository;
        _settingProvider = settingProvider;
        _backgroundJobManager = backgroundJobManager;
        _recipientResolver = recipientResolver;
        _logger = logger;
    }

    [UnitOfWork]
    public virtual async Task HandleEventAsync(AppointmentSubmittedEto eventData)
    {
        // B15 (2026-05-07): the newer BookingSubmissionEmailHandler (in
        // Application/Notifications/Handlers/) is the OLD-parity-correct
        // submission email path -- it uses the seeded
        // PatientAppointmentPending HTML template, the per-tenant CC list,
        // and the shared recipient resolver. This older inline-HTML
        // handler was an early W1-2 implementation that became redundant
        // once W2-10 introduced the resolver-based handler. Both still
        // subscribe to AppointmentSubmittedEto, so every booking fired
        // two emails per stakeholder ("BookingSubmitted/Pending/" + this
        // one's "Submission/<role>/").
        //
        // Per the Phase 1 email-scope directive, return early so only the
        // template-driven handler fires. The body is preserved below in
        // case we need to re-enable for a fallback scenario.
        await Task.CompletedTask;
        return;
#pragma warning disable CS0162 // unreachable code -- intentional, preserved for re-enable
        var bookerUser = await _identityUserRepository.FindAsync(eventData.BookerUserId);
        var patient = await _patientRepository.FindAsync(eventData.PatientId);
        var bookerName = ResolveBookerName(bookerUser, patient);
        var patientName = ResolvePatientName(patient);
        var dateLine = eventData.AppointmentDate.ToString("MMM d, yyyy h:mm tt");

        // W2-10: fan out to all parties via the shared recipient resolver.
        // Replaces W1-2's two-recipient direct enqueue (office + booker).
        // If the resolver returns 0 recipients (unexpected -- usually means
        // the OfficeEmail setting is unset AND the booker has no email),
        // fall back to the W1-2 paths so the demo still emits something.
        var recipients = await _recipientResolver.ResolveAsync(eventData.AppointmentId, NotificationKind.Submitted);
        if (recipients.Count == 0)
        {
            _logger.LogWarning(
                "SubmissionEmailHandler: resolver returned 0 recipients for appointment {AppointmentId}; falling back to W1-2 office+booker path.",
                eventData.AppointmentId);
            await SendOfficeEmailAsync(eventData, bookerName, patientName, dateLine);
            await SendBookerConfirmationAsync(eventData, bookerUser, patient, dateLine);
            return;
        }

        // S-6.1 / S-6.2 / S-6.3: per-recipient template branching. Each
        // recipient's args carries IsRegistered (set by the resolver from a
        // tenant-scoped IdentityUser email lookup) and TenantName (from
        // CurrentTenant.Name). Registered users see a "log in to view" body
        // pointing at the Angular portal. Non-registered users see a
        // "register as <role>" body pointing at AuthServer's /Account/Register
        // with `?__tenant=<TenantName>&email=<email>` so the tenant is locked
        // and the email is pre-filled. Office mailbox + the booker's
        // confirmation email keep their existing wording (verified
        // "appointment requested" phrasing per S-6.3).
        var portalBaseUrl = await _settingProvider.GetOrNullAsync(
            CaseEvaluationSettings.NotificationsPolicy.PortalBaseUrl);
        var authServerBaseUrl = await _settingProvider.GetOrNullAsync(
            CaseEvaluationSettings.NotificationsPolicy.AuthServerBaseUrl);

        foreach (var args in recipients)
        {
            (args.Subject, args.Body) = BuildPerRecipientTemplate(
                args,
                eventData,
                bookerName,
                patientName,
                dateLine,
                portalBaseUrl,
                authServerBaseUrl);
            args.IsBodyHtml = true;
            args.Context = $"Submission/{args.Role}/{eventData.AppointmentId}";
            await _backgroundJobManager.EnqueueAsync(args);
        }
#pragma warning restore CS0162
    }

    /// <summary>
    /// S-6.1 / S-6.2 / S-6.3: returns subject + HTML body tailored to a single
    /// recipient. Branches on (Role, IsRegistered):
    ///   - OfficeAdmin: "new appointment request" with portal queue link.
    ///   - Booker / Patient (registered): "we received your appointment request"
    ///     with confirmation # and login link.
    ///   - AA / DA / CE registered: "appointment requested -- log in to view"
    ///     with login link.
    ///   - AA / DA / CE not registered: "appointment requested -- register as
    ///     [role] to view" with /Account/Register?__tenant=&email= link.
    /// All branches embed the RequestConfirmationNumber and the appointment
    /// date line per S-6.2.
    /// </summary>
    private static (string Subject, string Body) BuildPerRecipientTemplate(
        SendAppointmentEmailArgs args,
        AppointmentSubmittedEto eventData,
        string bookerName,
        string patientName,
        string dateLine,
        string? portalBaseUrl,
        string? authServerBaseUrl)
    {
        var confirmationNumber = eventData.RequestConfirmationNumber;
        var role = args.Role ?? RecipientRole.Patient;

        if (role == RecipientRole.OfficeAdmin)
        {
            var subject = $"New appointment request {confirmationNumber}";
            var body = BuildHtml(
                title: "A new appointment request was submitted",
                intro: $"Confirmation #{confirmationNumber} requested for {dateLine}.",
                details:
                    $"<p><strong>Booker:</strong> {WebEncode(bookerName)}<br>" +
                    $"<strong>Patient:</strong> {WebEncode(patientName)}</p>" +
                    "<p>Open the appointments queue in the patient portal to review the request and respond.</p>" +
                    BuildLoginCta(portalBaseUrl));
            return (subject, body);
        }

        if (args.IsRegistered)
        {
            var subject = $"Appointment requested - {confirmationNumber}";
            var body = BuildHtml(
                title: "An appointment was requested",
                intro: $"Confirmation #{confirmationNumber} requested for {dateLine}.",
                details:
                    $"<p><strong>Booker:</strong> {WebEncode(bookerName)}<br>" +
                    $"<strong>Patient:</strong> {WebEncode(patientName)}</p>" +
                    $"<p>You are listed as the {WebEncode(RoleDisplayName(role))} on this appointment. Log in to the patient portal to view the request, see updates, and receive scheduling notifications.</p>" +
                    BuildLoginCta(portalBaseUrl));
            return (subject, body);
        }

        // Not registered -- "register as [role]" path.
        var registerSubject = $"Appointment requested - register to view {confirmationNumber}";
        var registerCta = BuildRegisterCta(authServerBaseUrl, args.TenantName, args.To, role);
        var registerBody = BuildHtml(
            title: "An appointment was requested",
            intro: $"Confirmation #{confirmationNumber} requested for {dateLine}.",
            details:
                $"<p><strong>Booker:</strong> {WebEncode(bookerName)}<br>" +
                $"<strong>Patient:</strong> {WebEncode(patientName)}</p>" +
                $"<p>You are listed as the {WebEncode(RoleDisplayName(role))} on this appointment but you do not yet have a portal login for this practice. Register below to view this and future appointments where you are involved.</p>" +
                registerCta);
        return (registerSubject, registerBody);
    }

    private static string RoleDisplayName(RecipientRole role) => role switch
    {
        RecipientRole.OfficeAdmin => "office",
        RecipientRole.Patient => "patient",
        RecipientRole.ApplicantAttorney => "applicant attorney",
        RecipientRole.DefenseAttorney => "defense attorney",
        RecipientRole.ClaimExaminer => "claim examiner",
        _ => "party",
    };

    private static string BuildLoginCta(string? portalBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(portalBaseUrl))
        {
            return string.Empty;
        }
        var url = portalBaseUrl.TrimEnd('/');
        return
            "<p style=\"margin-top: 20px;\">" +
            $"<a href=\"{WebEncode(url)}\" style=\"background:#0d6efd;color:#fff;padding:10px 20px;text-decoration:none;border-radius:4px;\">" +
            "Open patient portal" +
            "</a></p>";
    }

    private static string BuildRegisterCta(
        string? authServerBaseUrl,
        string? tenantName,
        string email,
        RecipientRole role)
    {
        if (string.IsNullOrWhiteSpace(authServerBaseUrl))
        {
            return string.Empty;
        }
        var baseUrl = authServerBaseUrl.TrimEnd('/');
        var query = new System.Text.StringBuilder("?");
        if (!string.IsNullOrWhiteSpace(tenantName))
        {
            query.Append("__tenant=").Append(System.Net.WebUtility.UrlEncode(tenantName)).Append('&');
        }
        if (!string.IsNullOrWhiteSpace(email))
        {
            query.Append("email=").Append(System.Net.WebUtility.UrlEncode(email)).Append('&');
        }
        // Trailing separator from the optional appends.
        var queryString = query.ToString().TrimEnd('?', '&');
        var url = $"{baseUrl}/Account/Register{queryString}";
        return
            "<p style=\"margin-top: 20px;\">" +
            $"<a href=\"{WebEncode(url)}\" style=\"background:#0d6efd;color:#fff;padding:10px 20px;text-decoration:none;border-radius:4px;\">" +
            $"Register as {WebEncode(RoleDisplayName(role))}" +
            "</a></p>" +
            "<p style=\"color:#888;font-size:0.85em;\">After registering, log in to view this appointment on the patient portal.</p>";
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
