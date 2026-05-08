using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.DoctorAvailabilities;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using HealthcareSupport.CaseEvaluation.SystemParameters;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
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
    private readonly ISystemParameterRepository _systemParameterRepository;
    private readonly IdentityUserManager _userManager;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<BookingSubmissionEmailHandler> _logger;

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
        ISystemParameterRepository systemParameterRepository,
        IdentityUserManager userManager,
        ICurrentTenant currentTenant,
        ILogger<BookingSubmissionEmailHandler> logger)
    {
        _dispatcher = dispatcher;
        _contextResolver = contextResolver;
        _recipientResolver = recipientResolver;
        _appointmentRepository = appointmentRepository;
        _doctorAvailabilityRepository = doctorAvailabilityRepository;
        _systemParameterRepository = systemParameterRepository;
        _userManager = userManager;
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

            // B15-followup (2026-05-07): the PatientAppointmentPending
            // stakeholder dispatch (subject "Your appointment request has
            // been Pending") is the duplicate Adrian flagged. The OLD-parity
            // "appointment requested" stakeholder email is delivered by the
            // Domain SubmissionEmailHandler instead. Method body kept intact
            // below so this can be re-enabled if the stakeholder template
            // ever replaces the inline-HTML handler.
            //
            // await DispatchPendingToStakeholdersAsync(
            //     eventData, ctx, appointment, appointmentDate, appointmentFromTime, appointmentToTime);

            // OLD parity (P:\PatientPortalOld\...\AppointmentDomain.cs:935-951):
            // when the booker is an external user, also fan out
            // PatientAppointmentApproveReject to every Staff Supervisor +
            // Clinic Staff user in the tenant. Different recipient set than
            // the stakeholder email above, so it is not a duplicate.
            await DispatchApproveRejectToStaffWhenBookerIsExternalAsync(
                eventData, ctx, appointment, appointmentDate,
                appointmentFromTime, appointmentToTime);
        }
    }

    /// <summary>
    /// OLD :925-933 -- the unconditional Pending fan-out. Resolves
    /// stakeholders via the shared resolver, appends per-tenant
    /// <see cref="SystemParameter.CcEmailIds"/> entries as additional
    /// <see cref="RecipientRole.OfficeAdmin"/> recipients (NEW does not
    /// emit a CC header; each CC address becomes its own send so logging
    /// + retry is per-address). Empty stakeholder list short-circuits.
    /// </summary>
    private async Task DispatchPendingToStakeholdersAsync(
        AppointmentSubmittedEto eventData,
        DocumentEmailContext ctx,
        Appointment appointment,
        string appointmentDate,
        string appointmentFromTime,
        string appointmentToTime)
    {
        var resolverOutput = await _recipientResolver.ResolveAsync(
            eventData.AppointmentId, NotificationKind.Submitted);
        var stakeholders = resolverOutput
            .Where(r => !string.IsNullOrWhiteSpace(r.To))
            .Select(r => new NotificationRecipient(
                email: r.To, role: r.Role, isRegistered: r.IsRegistered))
            .ToList();

        await AppendCcRecipientsAsync(stakeholders);

        if (stakeholders.Count == 0)
        {
            _logger.LogInformation(
                "BookingSubmissionEmailHandler: no recipients for Pending appointment {AppointmentId}; skipping.",
                eventData.AppointmentId);
            return;
        }

        var vars = BuildVariables(
            ctx, appointment, appointmentDate, appointmentFromTime, appointmentToTime);

        await _dispatcher.DispatchAsync(
            templateCode: NotificationTemplateConsts.Codes.PatientAppointmentPending,
            recipients: stakeholders,
            variables: vars,
            contextTag: $"BookingSubmitted/Pending/{eventData.AppointmentId}");
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
    /// Appends per-tenant CC recipients from
    /// <see cref="SystemParameter.CcEmailIds"/> (semicolon-separated).
    /// Each CC address becomes its own <see cref="NotificationRecipient"/>
    /// with role <see cref="RecipientRole.OfficeAdmin"/>. Dedupes
    /// against any address already in <paramref name="recipients"/>
    /// so a CC address that's also a stakeholder doesn't double-send.
    /// </summary>
    private async Task AppendCcRecipientsAsync(List<NotificationRecipient> recipients)
    {
        var systemParameter = await _systemParameterRepository.GetCurrentTenantAsync();
        if (systemParameter == null || string.IsNullOrWhiteSpace(systemParameter.CcEmailIds))
        {
            return;
        }

        var existing = new HashSet<string>(
            recipients.Select(r => r.Email),
            StringComparer.OrdinalIgnoreCase);

        var ccAddresses = systemParameter.CcEmailIds
            .Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(a => a.Trim())
            .Where(a => a.Length > 0);

        foreach (var address in ccAddresses)
        {
            if (existing.Add(address))
            {
                recipients.Add(new NotificationRecipient(
                    email: address,
                    role: RecipientRole.OfficeAdmin,
                    isRegistered: false));
            }
        }
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
