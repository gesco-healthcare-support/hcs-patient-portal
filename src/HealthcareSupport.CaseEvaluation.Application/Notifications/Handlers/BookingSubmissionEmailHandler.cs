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
using Volo.Abp;
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
///     Supervisor</c> + <c>Intake Staff</c> user in the tenant. Mirrors
///     OLD :935-951, the <c>currentUserTypeId == ExternalUser</c> guard.
///     Internal-staff bookings (Intake Staff / Staff Supervisor /
///     IT Admin / admin / Doctor) skip this leg because the booker IS
///     office staff and would email themselves.</item>
/// </list>
///
/// <para>2026-11-03: external-vs-internal is decided from the SUBMITTER's roles
/// (<c>DocumentEmailContext.CreatorIsStaff</c>, the appointment's creator), not from the
/// patient login in <c>AppointmentSubmittedEto.BookerUserId</c>. A staff booking sends no
/// request emails at all (the Approved email is the one notice), and unless the patient
/// submitted the request the patient is left off the request email (see
/// <see cref="PatientBeforeApprovalRule"/>).</para>
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
    private readonly OfficeNotificationRecipients _officeRecipients;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<BookingSubmissionEmailHandler> _logger;
    // BUG-029 v3 fix (2026-05-21): tenant-aware URL composition.
    private readonly IAccountUrlBuilder _accountUrlBuilder;

    // BUG-029 v3 fix (2026-05-21): Phase 2.A hardcoded Falkinstein
    // fallbacks removed. IAccountUrlBuilder throws a clear error if
    // App__SelfUrl / App__AngularUrl env var is missing.

    public BookingSubmissionEmailHandler(
        INotificationDispatcher dispatcher,
        DocumentEmailContextResolver contextResolver,
        IAppointmentRecipientResolver recipientResolver,
        IRepository<Appointment, Guid> appointmentRepository,
        IRepository<DoctorAvailability, Guid> doctorAvailabilityRepository,
        IRepository<Patient, Guid> patientRepository,
        OfficeNotificationRecipients officeRecipients,
        ICurrentTenant currentTenant,
        ILogger<BookingSubmissionEmailHandler> logger,
        IAccountUrlBuilder accountUrlBuilder)
    {
        _dispatcher = dispatcher;
        _contextResolver = contextResolver;
        _recipientResolver = recipientResolver;
        _appointmentRepository = appointmentRepository;
        _doctorAvailabilityRepository = doctorAvailabilityRepository;
        _patientRepository = patientRepository;
        _officeRecipients = officeRecipients;
        _currentTenant = currentTenant;
        _logger = logger;
        _accountUrlBuilder = accountUrlBuilder;
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

            // 2026-11-03: a booking made by a staff member is auto-approved right after it is submitted,
            // so the office sends ONE set of emails -- the Approved email to the parties, the inbox and
            // the Intake Staff -- and none of the request emails. The submitter is the appointment's
            // creator, not the patient login in IdentityUserId.
            if (ctx.CreatorIsStaff)
            {
                _logger.LogInformation(
                    "BookingSubmissionEmailHandler: appointment {AppointmentId} was booked by staff; "
                    + "request emails skipped (the approval email is the one notice).",
                    eventData.AppointmentId);
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

            // E1 (2026-06-03): single-message ex-parte addressing. The
            // appointment-request notice is ONE email To the booker with the
            // other parties CC'd, using a shared non-role-aware body; the office
            // mailbox keeps its own separate notice. Supersedes the earlier
            // per-recipient role-aware fan-out (Decision 2.1, 2026-05-08) --
            // separate per-party emails for the same notice are ex-parte
            // communication.
            try
            {
                await DispatchAppointmentRequestedAsync(eventData, ctx, appointment);

                // OLD parity (P:\PatientPortalOld\...\AppointmentDomain.cs:935-951):
                // when the booker is an external user, also fan out
                // PatientAppointmentApproveReject to every Staff Supervisor +
                // Intake Staff user in the tenant. Different recipient set than
                // the AppointmentRequested fan-out above, so it is not a duplicate.
                await DispatchApproveRejectToStaffWhenBookerIsExternalAsync(
                    eventData, ctx, appointment, appointmentDate,
                    appointmentFromTime, appointmentToTime);
            }
            catch (BusinessException ex)
                when (ex.Code == CaseEvaluationDomainErrorCodes.NotificationTemplateNotFound)
            {
                // 2026-05-13: same tolerance as StatusChangeEmailHandler. A
                // missing or inactive template must NOT block the CreateAsync
                // UoW; the email is a side effect, the appointment write
                // already committed. Log Warning so monitoring surfaces the
                // gap.
                _logger.LogWarning(
                    ex,
                    "BookingSubmissionEmailHandler: template missing for appointment {AppointmentId}; email skipped. Detail: {Detail}",
                    eventData.AppointmentId,
                    ex.Data.Contains("templateCode") ? ex.Data["templateCode"] : "(unspecified)");
            }
        }
    }

    /// <summary>
    /// E1 (2026-06-03) -- "Appointment Requested" ex-parte single-message
    /// dispatch. Resolves stakeholders via
    /// <see cref="IAppointmentRecipientResolver"/>, then sends ONE email
    /// addressed To the booker (or, if the booker is not a party, the patient)
    /// with every other party CC'd, rendered once from the shared
    /// non-role-aware
    /// <see cref="NotificationTemplateConsts.Codes.AppointmentRequestedRegistered"/>
    /// body ("log in or register to view" + a tenant login link). The office
    /// mailbox (<see cref="RecipientRole.OfficeAdmin"/>) is NOT a party: it
    /// keeps its own separate internal "new request" notice via
    /// <see cref="NotificationTemplateConsts.Codes.AppointmentRequestedOffice"/>.
    ///
    /// <para>Supersedes Decision 2.1 (2026-05-08), which sent a separate
    /// role-aware email per recipient -- separate per-party emails for the same
    /// notice are ex-parte communication. The per-recipient Unregistered
    /// "register as [role]" template is no longer used by this flow.</para>
    /// </summary>
    private async Task DispatchAppointmentRequestedAsync(
        AppointmentSubmittedEto eventData,
        DocumentEmailContext ctx,
        Appointment appointment)
    {
        var resolverOutput = await _recipientResolver.ResolveAsync(
            eventData.AppointmentId, NotificationKind.Submitted);
        // 2026-11-03: unless the patient submitted this request, the patient hears nothing until approval.
        // 2026-11-04: the case followers (external booker, accessors) are copied like the parties; a
        // Patient-role accessor follows the same patient rule.
        var recipients = PatientBeforeApprovalRule.Apply(
            CaseFollowers.Append(
                resolverOutput.Where(r => !string.IsNullOrWhiteSpace(r.To)), ctx, ctx.PatientIsSubmitter),
            ctx);

        if (recipients.Count == 0)
        {
            _logger.LogInformation(
                "BookingSubmissionEmailHandler: no recipients for AppointmentRequested fan-out, appointment {AppointmentId}; skipping.",
                eventData.AppointmentId);
            return;
        }

        // Resolve the URLs once per dispatch via IAccountUrlBuilder. The
        // builder pulls tenant Name from the explicit eventData.TenantId
        // rather than the unreliable _currentTenant.Name (null inside the
        // Change(tenantId) scope opened by the calling handler).
        // 2026-07-23: the office "new request" notice below is staff-only, so its portal
        // link goes to the HOST portal (admin.<base>) where staff work. The external-party
        // notice uses authServerBaseUrl (tenant login) and stays {tenant}.<base>.
        var officePortalUrl = await _accountUrlBuilder.BuildPortalRootUrlAsync(null);
        var authServerBaseUrl = await _accountUrlBuilder.BuildAuthServerRootUrlAsync(eventData.TenantId);

        // Build the booker name + patient name once -- both are stable
        // across all recipient variants and used by every template body.
        var patient = await _patientRepository.FindAsync(eventData.PatientId);
        var bookerName = ResolveBookerName(ctx.BookerFullName, ctx.BookerEmail, patient);
        var patientName = ResolvePatientName(patient);
        var dateLine = appointment.AppointmentDate.ToString("MMM d, yyyy h:mm tt");

        // E1 (2026-06-03) -- ex-parte single-message addressing (supersedes
        // Decision 2.1's per-recipient fan-out: separate per-party emails for
        // the SAME notice are ex-parte communication). The notice goes out as
        // ONE email addressed To the booker with the other parties CC'd, using
        // a single non-role-aware body. The office mailbox is NOT a party -- it
        // keeps its own separate internal "new request" notice.
        // Phase 4 (C3/D3): anchor To on the promoted attorney when the creator
        // (firm/paralegal booker) holds that attorney role; otherwise the existing
        // booker email. The promoted AA/DA email is a resolved party, so To binds
        // to it and the other parties + creator are CC'd.
        var addressing = PartitionAppointmentRequested(
            recipients, ctx.PrimaryRecipientEmail ?? ctx.BookerEmail);

        if (addressing.To != null)
        {
            // Shared, non-role-aware body (E2): "log in or register to view"
            // with a tenant login link. The body is rendered once and shared by
            // every recipient on the message, so it carries NO per-recipient
            // register pre-fill (LoginUrl is tenant-only, no email pre-fill).
            var sharedVariables = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["AppointmentRequestConfirmationNumber"] = eventData.RequestConfirmationNumber,
                ["AppointmentDateTime"] = dateLine,
                // Phase 4 (D5): greet the promoted attorney when applicable, else
                // the booker. Only the To-party (shared) notice is promoted; the
                // separate office notice below keeps the booker name.
                ["BookerFullName"] = !string.IsNullOrWhiteSpace(ctx.GreetingName)
                    ? ctx.GreetingName
                    : bookerName,
                ["PatientFirstName"] = ctx.PatientFirstName ?? patientName,
                ["PatientLastName"] = ctx.PatientLastName ?? string.Empty,
                ["LoginUrl"] = BuildLoginUrl(authServerBaseUrl, addressing.To.TenantName, string.Empty),
            };
            AddBrandPlaceholders(sharedVariables);

            var toRecipient = new NotificationRecipient(
                email: addressing.To.To,
                role: addressing.To.Role,
                isRegistered: addressing.To.IsRegistered);
            var ccRecipients = addressing.Cc
                .Select(r => new NotificationRecipient(
                    email: r.To, role: r.Role, isRegistered: r.IsRegistered))
                .ToList();

            // Phase 4 (C3/D3): CC the firm/paralegal creator when promoted -- they
            // are often not a resolved party (a different attorney email is named,
            // and when the patient has a login appointment.IdentityUserId = patient).
            // Skip when the creator IS the To (solo attorney) or already CC'd.
            // Scoped to the promoted case so non-attorney bookers are unchanged.
            if (ctx.IsPromoted
                && !string.IsNullOrWhiteSpace(ctx.CreatorEmail)
                && !string.Equals(ctx.CreatorEmail, toRecipient.Email, StringComparison.OrdinalIgnoreCase)
                && !ccRecipients.Any(r => string.Equals(r.Email, ctx.CreatorEmail, StringComparison.OrdinalIgnoreCase)))
            {
                // Role is CC metadata only; OfficeAdmin mirrors how the codebase
                // tags appended non-party CC recipients.
                ccRecipients.Add(new NotificationRecipient(
                    email: ctx.CreatorEmail, role: RecipientRole.OfficeAdmin, isRegistered: true));
            }

            await _dispatcher.DispatchToWithCcAsync(
                templateCode: NotificationTemplateConsts.Codes.AppointmentRequestedRegistered,
                to: toRecipient,
                cc: ccRecipients,
                variables: sharedVariables,
                contextTag: $"AppointmentRequested/{eventData.AppointmentId}");
        }

        // Office mailbox: separate internal "new request" queue notice
        // (unchanged -- office is internal staff, not an ex-parte party).
        foreach (var office in addressing.Office)
        {
            var officeVariables = BuildAppointmentRequestedVariables(
                eventData, ctx, office, bookerName, patientName, dateLine,
                officePortalUrl, authServerBaseUrl);
            var officeRecipient = new[]
            {
                new NotificationRecipient(
                    email: office.To, role: office.Role, isRegistered: office.IsRegistered),
            };
            await _dispatcher.DispatchAsync(
                templateCode: NotificationTemplateConsts.Codes.AppointmentRequestedOffice,
                recipients: officeRecipient,
                variables: officeVariables,
                contextTag: $"AppointmentRequested/Office/{eventData.AppointmentId}");
        }
    }

    /// <summary>
    /// E1 (2026-06-03): split resolved recipients into the ex-parte addressing
    /// for the appointment-request notice. Extracted <c>internal static</c> for
    /// unit-testability (matches the BuildLoginUrl / BuildRegisterUrl pattern).
    /// Rules: the office mailbox (<see cref="RecipientRole.OfficeAdmin"/>) is
    /// NOT a party and is returned separately; To = the booker if the booker's
    /// email is among the parties, else the patient-role party, else the first
    /// party; Cc = every other party (the To address excluded).
    /// </summary>
    internal static AppointmentRequestedAddressing PartitionAppointmentRequested(
        IReadOnlyList<SendAppointmentEmailArgs> recipients,
        string? bookerEmail)
    {
        var office = recipients
            .Where(r => r.Role == RecipientRole.OfficeAdmin)
            .ToList();
        var parties = recipients
            .Where(r => (r.Role ?? RecipientRole.Patient) != RecipientRole.OfficeAdmin)
            .ToList();

        if (parties.Count == 0)
        {
            return new AppointmentRequestedAddressing(null, new List<SendAppointmentEmailArgs>(), office);
        }

        var to =
            (string.IsNullOrWhiteSpace(bookerEmail)
                ? null
                : parties.FirstOrDefault(r =>
                    string.Equals(r.To, bookerEmail, StringComparison.OrdinalIgnoreCase)))
            ?? parties.FirstOrDefault(r => (r.Role ?? RecipientRole.Patient) == RecipientRole.Patient)
            ?? parties[0];

        var cc = parties.Where(r => !ReferenceEquals(r, to)).ToList();
        return new AppointmentRequestedAddressing(to, cc, office);
    }

    /// <summary>
    /// E1 (2026-06-03): result of <see cref="PartitionAppointmentRequested"/> --
    /// the single To party, the CC'd parties, and the (separate) office mailbox
    /// recipients for the appointment-request notice.
    /// </summary>
    internal sealed record AppointmentRequestedAddressing(
        SendAppointmentEmailArgs? To,
        IReadOnlyList<SendAppointmentEmailArgs> Cc,
        IReadOnlyList<SendAppointmentEmailArgs> Office);

    /// <summary>
    /// Phase 2.A: build the variable bag for the per-recipient
    /// AppointmentRequested templates. Includes the booker / patient name
    /// + role display + per-recipient login or register URL. Brand
    /// placeholders stay empty until per-tenant branding ships.
    /// </summary>
    private static Dictionary<string, object?> BuildAppointmentRequestedVariables(
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
        var loginUrl = BuildLoginUrl(authServerBaseUrl, args.TenantName, args.To);

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
            // Issue 2.4 (2026-05-12) — LoginUrl carries __tenant + email
            // pre-fill so already-registered recipients click straight
            // into a tenant-correct login screen.
            ["LoginUrl"] = loginUrl,
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
    internal static string BuildRegisterUrl(
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
        // Issue 2.4 (2026-05-12) — append role= so the Register page
        // pre-selects the user type dropdown. Matches the shape that
        // ExternalSignupAppService.BuildInviteUrl produces and that
        // global-scripts.js applyRolePrefill() consumes.
        var roleName = RoleToRoleName(role);
        if (!string.IsNullOrWhiteSpace(roleName))
        {
            query.Append("role=").Append(WebUtility.UrlEncode(roleName)).Append('&');
        }
        var queryString = query.ToString().TrimEnd('?', '&');
        return $"{baseUrl}/Account/Register{queryString}";
    }

    /// <summary>
    /// Issue 2.4 (2026-05-12) — Login URL for registered recipients.
    /// Carries <c>__tenant + email</c> pre-fill (no role; login doesn't
    /// pick role). When the recipient clicks the email's Login button
    /// they land on a tenant-correct login form with their email already
    /// in place.
    /// </summary>
    internal static string BuildLoginUrl(
        string authServerBaseUrl,
        string? tenantName,
        string email)
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
        return $"{baseUrl}/Account/Login{queryString}";
    }

    /// <summary>
    /// Maps the recipient role to the canonical role-name string used
    /// by ExternalSignupAppService.EnsureRoleAsync and consumed by the
    /// AuthServer Register page's role-select pre-fill (global-scripts.js
    /// applyRolePrefill). Mirrors the values in
    /// CaseEvaluationConsts.Roles. OfficeAdmin maps to empty -- office
    /// recipients never receive a register link.
    /// </summary>
    private static string RoleToRoleName(RecipientRole role) => role switch
    {
        RecipientRole.Patient => "Patient",
        RecipientRole.ApplicantAttorney => "Applicant Attorney",
        RecipientRole.DefenseAttorney => "Defense Attorney",
        RecipientRole.ClaimExaminer => "Claim Examiner",
        _ => string.Empty,
    };

    /// <summary>
    /// Phase 2.A: tenant setting fallback. Reads via
    /// <see cref="ISettingProvider"/>; falls back to the documented
    // BUG-029 v3 fix (2026-05-21): ResolveSettingAsync helper removed.
    // URL resolution + tenant composition lives in IAccountUrlBuilder.

    /// <summary>
    /// Phase 2.A: human-friendly booker name. Pulls from the
    /// submitter's name first; falls back to the patient row
    /// when the booker has no display name (e.g. early-flow accounts).
    /// </summary>
    private static string ResolveBookerName(string? bookerFullName, string? bookerEmail, Patient? patient)
    {
        if (!string.IsNullOrWhiteSpace(bookerFullName))
        {
            return bookerFullName.Trim();
        }
        if (!string.IsNullOrWhiteSpace(bookerEmail))
        {
            return bookerEmail;
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
    /// the booker is an external user. Dispatches
    /// <c>PatientAppointmentApproveReject</c> to the active Intake Staff
    /// users in the current tenant (2026-11-02; was Supervisor + Intake).
    /// </summary>
    private async Task DispatchApproveRejectToStaffWhenBookerIsExternalAsync(
        AppointmentSubmittedEto eventData,
        DocumentEmailContext ctx,
        Appointment appointment,
        string appointmentDate,
        string appointmentFromTime,
        string appointmentToTime)
    {
        // The submitter is the appointment's creator; a staff submitter was already filtered out above.
        if (ctx.CreatorIsStaff)
        {
            return;
        }

        // 2026-11-02: the approve/reject request goes to the switched-on Intake Staff only. Staff
        // Supervisors get no per-event email and the per-office CcEmailIds list is no longer appended.
        var staffRecipients = await _officeRecipients.GetIntakeAsync();

        if (staffRecipients.Count == 0)
        {
            _logger.LogWarning(
                "BookingSubmissionEmailHandler: no active Intake Staff users in tenant; "
                + "nobody on staff is asked to approve appointment {AppointmentId}.",
                eventData.AppointmentId);
            return;
        }

        // 2026-10-27: staff work on the staff site, so the link goes there (as the office notice's has since
        // 2026-07-23); a staff login does not work on the office site.
        var vars = BuildVariables(
            ctx, appointment, appointmentDate, appointmentFromTime, appointmentToTime,
            portalUrl: await _accountUrlBuilder.BuildPortalRootUrlAsync(null));

        await _dispatcher.DispatchAsync(
            templateCode: NotificationTemplateConsts.Codes.PatientAppointmentApproveReject,
            recipients: staffRecipients,
            variables: vars,
            contextTag: $"BookingSubmitted/ApproveReject/{eventData.AppointmentId}");
    }

    /// <summary>
    /// Builds the OLD-verbatim variable bag for the booking-submission
    /// templates. Same shape as <c>StatusChangeEmailHandler.BuildVariables</c>
    /// minus the <c>InternalUserComments</c> + <c>RejectionNotes</c> keys
    /// (Pending / ApproveReject templates do not reference them) and plus
    /// <c>AppointmentToTime</c> (the ApproveReject template prints both
    /// FromTime and ToTime per OLD :944 and the seeded HTML).
    /// </summary>
    private static Dictionary<string, object?> BuildVariables(
        DocumentEmailContext ctx,
        Appointment appointment,
        string appointmentDate,
        string appointmentFromTime,
        string appointmentToTime,
        string portalUrl)
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
            portalUrl: portalUrl);

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
