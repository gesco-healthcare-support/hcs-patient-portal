using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.DoctorAvailabilities;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using HealthcareSupport.CaseEvaluation.Patients;
using HealthcareSupport.CaseEvaluation.Settings;
using HealthcareSupport.CaseEvaluation.SystemParameters;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Settings;
using Volo.Abp.Uow;

namespace HealthcareSupport.CaseEvaluation.Notifications.Handlers;

/// <summary>
/// Subscribes to <see cref="AppointmentSubmittedEto"/> and dispatches the
/// OLD-parity booking-submission email cascade. Mirrors OLD's Pending
/// branch at <c>P:\PatientPortalOld\PatientAppointment.Domain\AppointmentRequestModule\AppointmentDomain.cs</c>:925-952:
///
/// <list type="bullet">
///   <item><b>Always</b> -- dispatches
///     <c>PatientAppointmentPending</c> to every stakeholder
///     (booker + parties + accessors + office mailbox + per-tenant
///     <see cref="SystemParameter.CcEmailIds"/> CC list). Mirrors OLD
///     :930-933, where the CC was the global <c>clinicStaffEmail</c>
///     ServerSetting and the office mailbox was implicit through the
///     stakeholder stored proc; NEW splits the office mailbox into
///     <c>NotificationsPolicy.OfficeEmail</c> (handled by the resolver)
///     and the <see cref="SystemParameter.CcEmailIds"/> column
///     (handled here, semicolon-separated).</item>
///   <item><b>Only when the booker is an external user</b> -- dispatches
///     <c>PatientAppointmentApproveReject</c> to every <c>Staff
///     Supervisor</c> + <c>Clinic Staff</c> user in the tenant. Mirrors
///     OLD :935-951, the <c>currentUserTypeId == ExternalUser</c> guard.
///     Internal-staff bookings (Clinic Staff / Staff Supervisor /
///     IT Admin / admin / Doctor) skip this leg because the booker IS
///     office staff and would email themselves.</item>
/// </list>
///
/// <para>External-vs-internal classification uses
/// <see cref="BookingFlowRoles.IsInternalUserCaller"/> against the
/// booker's roles -- the canonical NEW signal that mirrors OLD's
/// <c>UserType</c> claim. Booker resolved by
/// <see cref="AppointmentSubmittedEto.BookerUserId"/>.</para>
/// </summary>
public class BookingSubmissionEmailHandler :
    ILocalEventHandler<AppointmentSubmittedEto>,
    ITransientDependency
{
    private readonly INotificationDispatcher _dispatcher;
    private readonly DocumentEmailContextResolver _contextResolver;
    private readonly IAppointmentRecipientResolver _recipientResolver;
    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly IRepository<DoctorAvailability, Guid> _doctorAvailabilityRepository;
    private readonly IRepository<Patient, Guid> _patientRepository;
    private readonly CcRecipientAppender _ccAppender;
    private readonly IdentityUserManager _userManager;
    private readonly ISettingProvider _settingProvider;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<BookingSubmissionEmailHandler> _logger;

    // Phase 2.A defaults (Adrian Decision A 2026-05-08): same fallbacks the
    // CaseEvaluationAccountEmailer uses, kept in sync so per-tenant URL
    // overrides are read from one settings surface.
    private const string DefaultPortalBaseUrl = "http://falkinstein.localhost:4200";
    private const string DefaultAuthServerBaseUrl = "http://falkinstein.localhost:44368";

    /// <summary>
    /// OLD :945 -- internal-staff recipients for the
    /// PatientAppointmentApproveReject email are limited to
    /// <c>StaffSupervisor + ClinicStaff</c>. Other internal roles
    /// (admin / IT Admin / Doctor) are intentionally excluded.
    /// </summary>
    private static readonly string[] StaffApprovalNotificationRoles =
    {
        "Staff Supervisor",
        "Clinic Staff",
    };

    public BookingSubmissionEmailHandler(
        INotificationDispatcher dispatcher,
        DocumentEmailContextResolver contextResolver,
        IAppointmentRecipientResolver recipientResolver,
        IRepository<Appointment, Guid> appointmentRepository,
        IRepository<DoctorAvailability, Guid> doctorAvailabilityRepository,
        IRepository<Patient, Guid> patientRepository,
        CcRecipientAppender ccAppender,
        IdentityUserManager userManager,
        ISettingProvider settingProvider,
        ICurrentTenant currentTenant,
        ILogger<BookingSubmissionEmailHandler> logger)
    {
        _dispatcher = dispatcher;
        _contextResolver = contextResolver;
        _recipientResolver = recipientResolver;
        _appointmentRepository = appointmentRepository;
        _doctorAvailabilityRepository = doctorAvailabilityRepository;
        _patientRepository = patientRepository;
        _ccAppender = ccAppender;
        _userManager = userManager;
        _settingProvider = settingProvider;
        _currentTenant = currentTenant;
        _logger = logger;
    }

    [UnitOfWork]
    public virtual async Task HandleEventAsync(AppointmentSubmittedEto eventData)
    {
        if (eventData == null)
        {
            return;
        }

        using (_currentTenant.Change(eventData.TenantId))
        {
            var ctx = await _contextResolver.ResolveAsync(
                eventData.AppointmentId, appointmentDocumentId: null);
            if (ctx == null)
            {
                _logger.LogWarning(
                    "BookingSubmissionEmailHandler: appointment {AppointmentId} not found; skipping.",
                    eventData.AppointmentId);
                return;
            }

            var appointment = await _appointmentRepository.FindAsync(eventData.AppointmentId);
            if (appointment == null)
            {
                return;
            }

            var availability = await _doctorAvailabilityRepository.FindAsync(
                appointment.DoctorAvailabilityId);

            // OLD-parity formats (:912-913):
            //   AppointmentDate    -> "MM-dd-yyyy"
            //   AppointmentFromTime -> "hh:mm tt"
            //   AppointmentToTime   -> "hh:mm tt" (used by ApproveReject template)
            var appointmentDate = appointment.AppointmentDate.ToString(
                "MM-dd-yyyy", CultureInfo.InvariantCulture);
            var appointmentFromTime = FormatTimeOnlyOrEmpty(availability?.FromTime);
            var appointmentToTime = FormatTimeOnlyOrEmpty(availability?.ToTime);

            // Phase 2.A (Category 2, 2026-05-08): per-recipient
            // "Appointment Requested" stakeholder fan-out. Replaces the
            // earlier inline-HTML implementation in Domain
            // SubmissionEmailHandler (now deleted) -- same role-aware
            // content (registered party gets "log in to view"; unregistered
            // AA/DA/CE gets "register as [role]" with a tenant-prefilled
            // AuthServer link), but rendered through the per-tenant
            // NotificationTemplate path. Three template codes
            // (AppointmentRequestedOffice / Registered / Unregistered)
            // partition the audience. NO CC on this fan-out per Adrian
            // override 2026-05-08.
            await DispatchAppointmentRequestedAsync(
                eventData, ctx, appointment, appointmentDate, appointmentFromTime);

            // OLD parity (P:\PatientPortalOld\...\AppointmentDomain.cs:935-951):
            // when the booker is an external user, also fan out
            // PatientAppointmentApproveReject to every Staff Supervisor +
            // Clinic Staff user in the tenant. Different recipient set than
            // the AppointmentRequested fan-out above, so it is not a duplicate.
            await DispatchApproveRejectToStaffWhenBookerIsExternalAsync(
                eventData, ctx, appointment, appointmentDate,
                appointmentFromTime, appointmentToTime);
        }
    }

    /// <summary>
    /// Phase 2.A (Category 2, 2026-05-08) -- per-recipient
    /// "Appointment Requested" stakeholder fan-out. For each recipient
    /// returned by <see cref="IAppointmentRecipientResolver"/>, classify
    /// into one of three audience buckets and dispatch the matching
    /// template code:
    /// <list type="bullet">
    ///   <item><b>Office mailbox</b> (<see cref="RecipientRole.OfficeAdmin"/>) ->
    ///         <see cref="NotificationTemplateConsts.Codes.AppointmentRequestedOffice"/>.
    ///         "New appointment request" + portal queue link.</item>
    ///   <item><b>Registered party</b> (any other role with
    ///         <c>IsRegistered=true</c>) ->
    ///         <see cref="NotificationTemplateConsts.Codes.AppointmentRequestedRegistered"/>.
    ///         "Log in to view" + portal CTA.</item>
    ///   <item><b>Unregistered party</b> (any other role with
    ///         <c>IsRegistered=false</c>) ->
    ///         <see cref="NotificationTemplateConsts.Codes.AppointmentRequestedUnregistered"/>.
    ///         "Register as [role]" + AuthServer register link with
    ///         <c>?__tenant=&amp;email=</c> pre-fill so the new account
    ///         lands in the right tenant + saves the typed-but-unregistered
    ///         email a re-entry.</item>
    /// </list>
    ///
    /// <para>NO CC on this fan-out per Adrian directive 2026-05-08
    /// (Decision 2.1). The office mailbox already receives its own
    /// dedicated email via the <see cref="RecipientRole.OfficeAdmin"/>
    /// path; appending <see cref="SystemParameter.CcEmailIds"/> entries
    /// would duplicate.</para>
    /// </summary>
    private async Task DispatchAppointmentRequestedAsync(
        AppointmentSubmittedEto eventData,
        DocumentEmailContext ctx,
        Appointment appointment,
        string appointmentDate,
        string appointmentFromTime)
    {
        var resolverOutput = await _recipientResolver.ResolveAsync(
            eventData.AppointmentId, NotificationKind.Submitted);
        var recipients = resolverOutput
            .Where(r => !string.IsNullOrWhiteSpace(r.To))
            .ToList();

        if (recipients.Count == 0)
        {
            _logger.LogInformation(
                "BookingSubmissionEmailHandler: no recipients for AppointmentRequested fan-out, appointment {AppointmentId}; skipping.",
                eventData.AppointmentId);
            return;
        }

        // Resolve the URLs once per dispatch -- both are tenant-scoped
        // settings that don't change per-recipient.
        var portalBaseUrl = await ResolveSettingAsync(
            CaseEvaluationSettings.NotificationsPolicy.PortalBaseUrl,
            DefaultPortalBaseUrl);
        var authServerBaseUrl = await ResolveSettingAsync(
            CaseEvaluationSettings.NotificationsPolicy.AuthServerBaseUrl,
            DefaultAuthServerBaseUrl);

        // Build the booker name + patient name once -- both are stable
        // across all recipient variants and used by every template body.
        var bookerUser = eventData.BookerUserId == Guid.Empty
            ? null
            : await _userManager.FindByIdAsync(eventData.BookerUserId.ToString());
        var patient = await _patientRepository.FindAsync(eventData.PatientId);
        var bookerName = ResolveBookerName(bookerUser, patient);
        var patientName = ResolvePatientName(patient);
        var dateLine = appointment.AppointmentDate.ToString("MMM d, yyyy h:mm tt");

        // Classify + dispatch per recipient. One DispatchAsync call per
        // recipient so each gets its own variable bag (RoleDisplayName
        // matters per recipient, RegisterUrl needs the recipient email).
        // Group by template code so the renderer's template-load happens
        // once per audience bucket rather than per-recipient -- the
        // dispatcher batches recipients sharing a (templateCode, variables)
        // tuple, but our variables differ per recipient (RoleDisplayName,
        // RegisterUrl) so we dispatch per-recipient.
        foreach (var args in recipients)
        {
            // SendAppointmentEmailArgs.Role is nullable -- the resolver
            // sets it for every stakeholder fan-out today, but defending
            // against null avoids a NRE if a future caller publishes a
            // role-less AppointmentSubmittedEto. Default to Patient (the
            // most-permissive booker fallback the deleted Domain handler
            // also used) which routes to the Registered/Unregistered
            // template variants based on IsRegistered.
            var role = args.Role ?? RecipientRole.Patient;
            var templateCode = ClassifyTemplateCode(role, args.IsRegistered);
            var variables = BuildAppointmentRequestedVariables(
                eventData,
                ctx,
                args,
                bookerName,
                patientName,
                dateLine,
                portalBaseUrl,
                authServerBaseUrl);
            var single = new[]
            {
                new NotificationRecipient(
                    email: args.To,
                    role: args.Role,
                    isRegistered: args.IsRegistered),
            };
            await _dispatcher.DispatchAsync(
                templateCode: templateCode,
                recipients: single,
                variables: variables,
                contextTag: $"AppointmentRequested/{args.Role}/{eventData.AppointmentId}");
        }
    }

    /// <summary>
    /// Phase 2.A: maps a recipient's (role, isRegistered) pair to the
    /// matching <see cref="NotificationTemplateConsts.Codes"/> entry.
    /// OfficeAdmin always lands on the Office template; other roles split
    /// on registration status.
    /// </summary>
    private static string ClassifyTemplateCode(RecipientRole role, bool isRegistered)
    {
        if (role == RecipientRole.OfficeAdmin)
        {
            return NotificationTemplateConsts.Codes.AppointmentRequestedOffice;
        }
        return isRegistered
            ? NotificationTemplateConsts.Codes.AppointmentRequestedRegistered
            : NotificationTemplateConsts.Codes.AppointmentRequestedUnregistered;
    }

    /// <summary>
    /// Phase 2.A: build the variable bag for the per-recipient
    /// AppointmentRequested templates. Includes the booker / patient name
    /// + role display + per-recipient login or register URL. Brand
    /// placeholders stay empty until per-tenant branding ships.
    /// </summary>
    private static IReadOnlyDictionary<string, object?> BuildAppointmentRequestedVariables(
        AppointmentSubmittedEto eventData,
        DocumentEmailContext ctx,
        SendAppointmentEmailArgs args,
        string bookerName,
        string patientName,
        string dateLine,
        string portalBaseUrl,
        string authServerBaseUrl)
    {
        // Same null-coalesce as the dispatch loop -- args.Role is nullable
        // on SendAppointmentEmailArgs but the helpers are non-nullable.
        var role = args.Role ?? RecipientRole.Patient;
        var roleDisplayName = ResolveRoleDisplayName(role);
        var portalUrl = portalBaseUrl.TrimEnd('/');
        var registerUrl = BuildRegisterUrl(authServerBaseUrl, args.TenantName, args.To, role);

        var vars = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["AppointmentRequestConfirmationNumber"] = eventData.RequestConfirmationNumber,
            ["AppointmentDateTime"] = dateLine,
            ["BookerFullName"] = bookerName,
            ["PatientFirstName"] = ctx.PatientFirstName ?? patientName,
            ["PatientLastName"] = ctx.PatientLastName ?? string.Empty,
            ["RoleDisplayName"] = roleDisplayName,
            ["PortalUrl"] = portalUrl,
            ["RegisterUrl"] = registerUrl,
        };
        AddBrandPlaceholders(vars);
        return vars;
    }

    /// <summary>
    /// Phase 2.A: human-readable role string used inside the template
    /// bodies ("You are listed as the [applicant attorney] on this
    /// appointment"). Mirrors the wording previously hardcoded in the
    /// Domain SubmissionEmailHandler.
    /// </summary>
    private static string ResolveRoleDisplayName(RecipientRole role) => role switch
    {
        RecipientRole.OfficeAdmin => "office",
        RecipientRole.Patient => "patient",
        RecipientRole.ApplicantAttorney => "applicant attorney",
        RecipientRole.DefenseAttorney => "defense attorney",
        RecipientRole.ClaimExaminer => "claim examiner",
        _ => "party",
    };

    /// <summary>
    /// Phase 2.A: build the AuthServer Register URL with
    /// <c>?__tenant=&amp;email=</c> pre-fill so an unregistered AA / DA /
    /// CE landing here from the email lands in the right tenant + has
    /// their email pre-populated. Mirrors the prior inline implementation
    /// in Domain SubmissionEmailHandler.BuildRegisterCta.
    /// </summary>
    private static string BuildRegisterUrl(
        string authServerBaseUrl,
        string? tenantName,
        string email,
        RecipientRole role)
    {
        var baseUrl = authServerBaseUrl.TrimEnd('/');
        var query = new StringBuilder("?");
        if (!string.IsNullOrWhiteSpace(tenantName))
        {
            query.Append("__tenant=").Append(WebUtility.UrlEncode(tenantName)).Append('&');
        }
        if (!string.IsNullOrWhiteSpace(email))
        {
            query.Append("email=").Append(WebUtility.UrlEncode(email)).Append('&');
        }
        var queryString = query.ToString().TrimEnd('?', '&');
        return $"{baseUrl}/Account/Register{queryString}";
    }

    /// <summary>
    /// Phase 2.A: tenant setting fallback. Reads via
    /// <see cref="ISettingProvider"/>; falls back to the documented
    /// dev-stack default when the setting is unset.
    /// </summary>
    private async Task<string> ResolveSettingAsync(string settingKey, string defaultValue)
    {
        var configured = await _settingProvider.GetOrNullAsync(settingKey);
        return string.IsNullOrWhiteSpace(configured) ? defaultValue : configured!;
    }

    /// <summary>
    /// Phase 2.A: human-friendly booker name. Pulls from the
    /// <see cref="IdentityUser"/> first; falls back to the patient row
    /// when the booker has no display name (e.g. early-flow accounts).
    /// </summary>
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
                return bookerUser.Email!;
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

    /// <summary>
    /// Phase 2.A: human-friendly patient name. Falls back when the patient
    /// row is missing or names are blank.
    /// </summary>
    private static string ResolvePatientName(Patient? patient)
    {
        if (patient == null)
        {
            return "(unknown patient)";
        }
        var name = $"{patient.FirstName} {patient.LastName}".Trim();
        return string.IsNullOrWhiteSpace(name) ? "(unnamed patient)" : name;
    }

    /// <summary>
    /// OLD :935-951 -- the conditional staff-blast that fires only when
    /// the booker is an external user. Resolves Staff Supervisor +
    /// Clinic Staff users in the current tenant, dispatches
    /// <c>PatientAppointmentApproveReject</c> to all of them.
    /// </summary>
    private async Task DispatchApproveRejectToStaffWhenBookerIsExternalAsync(
        AppointmentSubmittedEto eventData,
        DocumentEmailContext ctx,
        Appointment appointment,
        string appointmentDate,
        string appointmentFromTime,
        string appointmentToTime)
    {
        if (!await IsBookerExternalAsync(eventData.BookerUserId))
        {
            return;
        }

        var staffRecipients = await ResolveStaffApprovalRecipientsAsync(eventData.AppointmentId);
        if (staffRecipients.Count == 0)
        {
            _logger.LogInformation(
                "BookingSubmissionEmailHandler: no Staff Supervisor / Clinic Staff users in tenant; skipping ApproveReject for appointment {AppointmentId}.",
                eventData.AppointmentId);
            return;
        }

        // Phase 2.B (Adrian Decision 2.2 2026-05-08): plus office mailbox CC'd
        // on the staff blast. OLD's :950 SendSMTPMail overload at this leg
        // takes only 3 args (no CC) -- this is an explicit override of OLD
        // behavior to keep an off-staff office address in the loop.
        await _ccAppender.AppendAsync(
            staffRecipients,
            contextTagForLogging: $"ApproveReject/{eventData.AppointmentId}");

        var vars = BuildVariables(
            ctx, appointment, appointmentDate, appointmentFromTime, appointmentToTime);

        await _dispatcher.DispatchAsync(
            templateCode: NotificationTemplateConsts.Codes.PatientAppointmentApproveReject,
            recipients: staffRecipients,
            variables: vars,
            contextTag: $"BookingSubmitted/ApproveReject/{eventData.AppointmentId}");
    }

    /// <summary>
    /// Returns <c>true</c> when the booker holds zero internal roles
    /// (Patient / Adjuster / Applicant Attorney / Defense Attorney /
    /// Claim Examiner). Inverts <see cref="BookingFlowRoles.IsInternalUserCaller"/>.
    /// </summary>
    private async Task<bool> IsBookerExternalAsync(Guid bookerUserId)
    {
        if (bookerUserId == Guid.Empty)
        {
            return true;
        }
        var booker = await _userManager.FindByIdAsync(bookerUserId.ToString());
        if (booker == null)
        {
            // No booker row -- treat as external (OLD's UserClaim path
            // would have thrown earlier for a missing user; we log + skip
            // the staff blast defensively).
            _logger.LogDebug(
                "BookingSubmissionEmailHandler: booker {UserId} not found; treating as external.",
                bookerUserId);
            return true;
        }
        var roles = await _userManager.GetRolesAsync(booker);
        return !BookingFlowRoles.IsInternalUserCaller(roles);
    }

    /// <summary>
    /// Loads every Staff Supervisor + Clinic Staff user in the current
    /// tenant and packages them into <see cref="NotificationRecipient"/>
    /// rows. Dedupes on email (a user with both roles only gets one
    /// email).
    /// </summary>
    private async Task<List<NotificationRecipient>> ResolveStaffApprovalRecipientsAsync(Guid appointmentId)
    {
        var byEmail = new Dictionary<string, NotificationRecipient>(StringComparer.OrdinalIgnoreCase);
        foreach (var roleName in StaffApprovalNotificationRoles)
        {
            var users = await _userManager.GetUsersInRoleAsync(roleName);
            foreach (var user in users)
            {
                if (string.IsNullOrWhiteSpace(user.Email))
                {
                    _logger.LogDebug(
                        "BookingSubmissionEmailHandler: skipping {Role} user {UserId} -- empty email; appointment {AppointmentId}.",
                        roleName, user.Id, appointmentId);
                    continue;
                }
                byEmail[user.Email] = new NotificationRecipient(
                    email: user.Email,
                    role: RecipientRole.OfficeAdmin,
                    isRegistered: true);
            }
        }
        return byEmail.Values.ToList();
    }

    /// <summary>
    /// Builds the OLD-verbatim variable bag for the booking-submission
    /// templates. Same shape as <c>StatusChangeEmailHandler.BuildVariables</c>
    /// minus the <c>InternalUserComments</c> + <c>RejectionNotes</c> keys
    /// (Pending / ApproveReject templates do not reference them) and plus
    /// <c>AppointmentToTime</c> (the ApproveReject template prints both
    /// FromTime and ToTime per OLD :944 and the seeded HTML).
    /// </summary>
    private static IReadOnlyDictionary<string, object?> BuildVariables(
        DocumentEmailContext ctx,
        Appointment appointment,
        string appointmentDate,
        string appointmentFromTime,
        string appointmentToTime)
    {
        var baseVars = DocumentNotificationContext.BuildVariables(
            patientFirstName: ctx.PatientFirstName,
            patientLastName: ctx.PatientLastName,
            patientEmail: ctx.PatientEmail,
            requestConfirmationNumber: ctx.RequestConfirmationNumber,
            appointmentDate: appointment.AppointmentDate,
            claimNumber: ctx.ClaimNumber,
            wcabAdj: ctx.WcabAdj,
            documentName: null,
            rejectionNotes: null,
            clinicName: null,
            portalUrl: ctx.PortalBaseUrl);

        var vars = new Dictionary<string, object?>(baseVars, StringComparer.Ordinal)
        {
            // OLD-format date overrides BuildVariables's MM/dd/yyyy.
            ["AppointmentDate"] = appointmentDate,
            ["AppointmentFromTime"] = appointmentFromTime,
            ["AppointmentToTime"] = appointmentToTime,
            // Pending + ApproveReject templates do not reference
            // InternalUserComments / RejectionNotes; populate to empty
            // for safety so any future template revision that adds the
            // tokens does not render literal "##InternalUserComments##".
            ["InternalUserComments"] = string.Empty,
        };

        AddBrandPlaceholders(vars);
        return vars;
    }

    /// <summary>
    /// Per-tenant branding tokens the OLD HTML templates reference.
    /// Same placeholder set as <c>StatusChangeEmailHandler</c>; mirrored
    /// here rather than extracted because the two handlers are the only
    /// users today. Per-tenant branding wiring is tracked separately.
    /// </summary>
    private static void AddBrandPlaceholders(Dictionary<string, object?> vars)
    {
        vars["CompanyLogo"] = string.Empty;
        vars["lblHeaderTitle"] = string.Empty;
        vars["lblFooterText"] = string.Empty;
        vars["Email"] = string.Empty;
        vars["Skype"] = string.Empty;
        vars["ph_US"] = string.Empty;
        vars["fax"] = string.Empty;
        vars["imageInByte"] = string.Empty;
    }

    private static string FormatTimeOnlyOrEmpty(TimeOnly? time)
    {
        if (!time.HasValue)
        {
            return string.Empty;
        }
        return time.Value.ToString("hh:mm tt", CultureInfo.GetCultureInfo("en-US"));
    }
}
