using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.DoctorAvailabilities;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace HealthcareSupport.CaseEvaluation.Notifications.Handlers;

/// <summary>
/// Subscribes to <see cref="AppointmentStatusChangedEto"/> and dispatches
/// the OLD-parity stakeholder cascade through Phase 18's
/// <see cref="INotificationDispatcher"/>. Mirrors OLD's status-email
/// switch at <c>P:\PatientPortalOld\PatientAppointment.Domain\AppointmentRequestModule\AppointmentDomain.cs</c>:923-991:
///
/// <list type="bullet">
///   <item><b>Approved</b> -- fires TWO templated emails. First to every
///     stakeholder (booker + parties + accessors) using
///     <c>PatientAppointmentApprovedExt</c> with the
///     <c>InternalUserComments</c> wrapped in OLD's "Please note:" prefix
///     (mirrors OLD :970-980, the <c>internalUserUpdateStatus = false</c>
///     branch -- which in NEW Phase 12 always corresponds to the
///     stakeholder-side fan-out). Second to <c>PrimaryResponsibleUserId</c>
///     only, using <c>PatientAppointmentApprovedInternal</c> with the
///     comments wrapped in OLD's "Staff comments for an appointment:"
///     prefix (mirrors OLD :957-966, the responsible-user leg).</item>
///   <item><b>Rejected</b> -- fires ONE templated email to every
///     stakeholder using <c>PatientAppointmentRejected</c> with
///     <c>RejectionNotes</c> populated (mirrors OLD :984-991; OLD does
///     not CC clinicStaff on rejection -- line 990 calls the 3-arg
///     <c>SendSMTPMail</c> overload).</item>
/// </list>
///
/// <para>OLD CC behavior (clinicStaffEmail global) for Approved: not yet
/// reproduced here. <c>SystemParameter.CcEmailIds</c> is the per-tenant
/// equivalent in NEW; wiring CC into the dispatcher as additional
/// recipients is queued for the BookingSubmissionEmailHandler commit
/// (commit B), where the same parameter is more demo-relevant.</para>
///
/// <para>This handler replaces the prior inline-body version that lived
/// at <c>Domain\Appointments\Handlers\StatusChangeEmailHandler.cs</c>.
/// Domain-layer code cannot reference <see cref="INotificationDispatcher"/>
/// (which lives in Application.Contracts) per ABP's layered architecture,
/// so the migrated handler moved up to Application. The Domain-layer
/// file is removed in the same commit.</para>
/// </summary>
public class StatusChangeEmailHandler :
    ILocalEventHandler<AppointmentStatusChangedEto>,
    ITransientDependency
{
    private readonly INotificationDispatcher _dispatcher;
    private readonly DocumentEmailContextResolver _contextResolver;
    private readonly IAppointmentRecipientResolver _recipientResolver;
    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly IRepository<DoctorAvailability, Guid> _doctorAvailabilityRepository;
    private readonly IRepository<IdentityUser, Guid> _identityUserRepository;
    private readonly CcRecipientAppender _ccAppender;
    private readonly IdentityUserManager _userManager;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<StatusChangeEmailHandler> _logger;

    /// <summary>
    /// Phase 2.C (2026-05-08): NoShow recipients are limited to internal
    /// staff (StaffSupervisor + ClinicStaff) per OLD :1021 -- patient does
    /// NOT receive the no-show notification. Same role allow-list as
    /// <c>BookingSubmissionEmailHandler.StaffApprovalNotificationRoles</c>;
    /// kept duplicated here so each handler is self-contained.
    /// </summary>
    private static readonly string[] NoShowInternalRoles =
    {
        "Staff Supervisor",
        "Clinic Staff",
    };

    public StatusChangeEmailHandler(
        INotificationDispatcher dispatcher,
        DocumentEmailContextResolver contextResolver,
        IAppointmentRecipientResolver recipientResolver,
        IRepository<Appointment, Guid> appointmentRepository,
        IRepository<DoctorAvailability, Guid> doctorAvailabilityRepository,
        IRepository<IdentityUser, Guid> identityUserRepository,
        CcRecipientAppender ccAppender,
        IdentityUserManager userManager,
        ICurrentTenant currentTenant,
        ILogger<StatusChangeEmailHandler> logger)
    {
        _dispatcher = dispatcher;
        _contextResolver = contextResolver;
        _recipientResolver = recipientResolver;
        _appointmentRepository = appointmentRepository;
        _doctorAvailabilityRepository = doctorAvailabilityRepository;
        _identityUserRepository = identityUserRepository;
        _ccAppender = ccAppender;
        _userManager = userManager;
        _currentTenant = currentTenant;
        _logger = logger;
    }

    [UnitOfWork]
    public virtual async Task HandleEventAsync(AppointmentStatusChangedEto eventData)
    {
        if (eventData == null)
        {
            return;
        }
        // Phase 2.C (2026-05-08): the four deferred status emails ride on
        // the same handler -- one Eto, status-switched dispatch. Statuses
        // outside this set are intentionally unhandled (Phase 12 lifecycle
        // states like Billed / RescheduleRequested / CancellationRequested
        // either fire from their own handlers or are not emailed at all).
        if (!IsHandledStatus(eventData.ToStatus))
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
                    "StatusChangeEmailHandler: appointment {AppointmentId} not found; skipping {Status}.",
                    eventData.AppointmentId, eventData.ToStatus);
                return;
            }

            var appointment = await _appointmentRepository.FindAsync(eventData.AppointmentId);
            if (appointment == null)
            {
                return;
            }

            var availability = await _doctorAvailabilityRepository.FindAsync(
                appointment.DoctorAvailabilityId);

            // OLD-parity formats:
            //   AppointmentDate    -> "MM-dd-yyyy" (NOT BuildVariables's MM/dd/yyyy)
            //   AppointmentFromTime -> "hh:mm tt" (12h with AM/PM, en-US)
            // (AppointmentDocumentDomain.cs:912-913.) Rebuilt locally so the
            // OLD-verbatim seeded HTML bodies render the same character sequence.
            var appointmentDate = appointment.AppointmentDate.ToString(
                "MM-dd-yyyy", CultureInfo.InvariantCulture);
            var appointmentFromTime = FormatTimeOnlyOrEmpty(availability?.FromTime);

            // IsHandledStatus filtered out null + unhandled statuses already;
            // null-forgiving the .Value access since the compiler doesn't
            // track the post-condition.
            var status = eventData.ToStatus!.Value;

            // NoShow goes to internal staff only (OLD :1021), so its
            // recipient set is computed below from IdentityUserManager
            // rather than from the resolver. The other four (Approved,
            // Rejected, CheckedIn, CheckedOut, CancelledNoBill) all use
            // the standard stakeholder fan-out.
            List<NotificationRecipient> stakeholders;
            if (status == AppointmentStatusType.NoShow)
            {
                stakeholders = new List<NotificationRecipient>();
            }
            else
            {
                var stakeholderArgs = await _recipientResolver.ResolveAsync(
                    eventData.AppointmentId, MapKind(status));
                stakeholders = stakeholderArgs
                    .Where(r => !string.IsNullOrWhiteSpace(r.To))
                    .Select(r => new NotificationRecipient(
                        email: r.To, role: r.Role, isRegistered: r.IsRegistered))
                    .ToList();
            }

            try
            {
                switch (status)
                {
                    case AppointmentStatusType.Approved:
                        await DispatchApprovedAsync(
                            eventData, ctx, appointment, appointmentDate, appointmentFromTime, stakeholders);
                        break;

                    case AppointmentStatusType.Rejected:
                        await DispatchRejectedAsync(
                            eventData, ctx, appointment, appointmentDate, appointmentFromTime, stakeholders);
                        break;

                    case AppointmentStatusType.CheckedIn:
                        await DispatchCheckedInAsync(
                            eventData, ctx, appointment, appointmentDate, appointmentFromTime, stakeholders);
                        break;

                    case AppointmentStatusType.CheckedOut:
                        await DispatchCheckedOutAsync(
                            eventData, ctx, appointment, appointmentDate, appointmentFromTime, stakeholders);
                        break;

                    case AppointmentStatusType.NoShow:
                        await DispatchNoShowAsync(
                            eventData, ctx, appointment, appointmentDate, appointmentFromTime);
                        break;

                    case AppointmentStatusType.CancelledNoBill:
                        await DispatchCancelledNoBillAsync(
                            eventData, ctx, appointment, appointmentDate, appointmentFromTime, stakeholders);
                        break;
                }
            }
            catch (BusinessException ex)
                when (ex.Code == CaseEvaluationDomainErrorCodes.NotificationTemplateNotFound)
            {
                // 2026-05-13: a status-change handler whose template is not
                // seeded (or deactivated) must NOT block the upstream
                // operation (booking, approval, etc.). The event handler is
                // a side effect; the appointment write committed already.
                // Log at Warning so monitoring surfaces it; let the handler
                // return so the in-process event bus does not surface the
                // exception back to the dispatching UoW.
                _logger.LogWarning(
                    "StatusChangeEmailHandler: template missing for appointment {AppointmentId} status {Status}; email skipped. Detail: {Detail}",
                    eventData.AppointmentId,
                    eventData.ToStatus,
                    ex.Data.Contains("templateCode") ? ex.Data["templateCode"] : "(unspecified)");
            }
        }
    }

    /// <summary>
    /// Phase 2.C (2026-05-08): the six statuses this handler covers. Any
    /// other status passes through silently.
    /// </summary>
    private static bool IsHandledStatus(AppointmentStatusType? status) => status switch
    {
        AppointmentStatusType.Approved => true,
        AppointmentStatusType.Rejected => true,
        AppointmentStatusType.CheckedIn => true,
        AppointmentStatusType.CheckedOut => true,
        AppointmentStatusType.NoShow => true,
        AppointmentStatusType.CancelledNoBill => true,
        _ => false,
    };

    private async Task DispatchApprovedAsync(
        AppointmentStatusChangedEto eventData,
        DocumentEmailContext ctx,
        Appointment appointment,
        string appointmentDate,
        string appointmentFromTime,
        List<NotificationRecipient> stakeholders)
    {
        // OLD :968-981 (the !internalUserUpdateStatus branch -- in NEW
        // this is always "the stakeholder side" because all approves
        // run through internal staff). Comment prefix is "Please note:".
        // Phase 2.B: per-tenant CC list appended (OLD :954 reads
        // clinicStaffEmail and passes as the 4th-arg emailCC at :980).
        if (stakeholders.Count > 0)
        {
            await _ccAppender.AppendAsync(
                stakeholders,
                contextTagForLogging: $"Approved/Stakeholders/{eventData.AppointmentId}");

            var extVars = BuildVariables(
                ctx,
                appointment,
                appointmentDate,
                appointmentFromTime,
                wrapInternalComments: WrapPleaseNote(appointment.InternalUserComments),
                rejectionNotes: null);
            await _dispatcher.DispatchAsync(
                templateCode: NotificationTemplateConsts.Codes.PatientAppointmentApprovedExt,
                recipients: stakeholders,
                variables: extVars,
                contextTag: $"StatusChange/Approved/Stakeholders/{eventData.AppointmentId}");
        }
        else
        {
            _logger.LogInformation(
                "StatusChangeEmailHandler: no stakeholders for Approved appointment {AppointmentId}; skipping ext send.",
                eventData.AppointmentId);
        }

        // OLD :953-967 (the internalUserUpdateStatus branch -- the
        // PrimaryResponsibleUserId leg). Comment prefix is
        // "Staff comments for an appointment:".
        if (!appointment.PrimaryResponsibleUserId.HasValue
            || appointment.PrimaryResponsibleUserId.Value == Guid.Empty)
        {
            return;
        }

        var responsibleUser = await _identityUserRepository.FindAsync(
            appointment.PrimaryResponsibleUserId.Value);
        if (responsibleUser == null || string.IsNullOrWhiteSpace(responsibleUser.Email))
        {
            _logger.LogWarning(
                "StatusChangeEmailHandler: PrimaryResponsibleUserId {UserId} for appointment {AppointmentId} has no resolvable email; skipping internal send.",
                appointment.PrimaryResponsibleUserId, eventData.AppointmentId);
            return;
        }

        var internalRecipients = new List<NotificationRecipient>
        {
            new(
                email: responsibleUser.Email!,
                role: RecipientRole.OfficeAdmin,
                isRegistered: true),
        };

        // Phase 2.B: per-tenant CC list also applies to the responsible-user
        // leg (OLD :954 sets emailCC once and reuses it across both internal
        // and ext branches at :966 and :980).
        await _ccAppender.AppendAsync(
            internalRecipients,
            contextTagForLogging: $"Approved/Responsible/{eventData.AppointmentId}");

        var internalVars = BuildVariables(
            ctx,
            appointment,
            appointmentDate,
            appointmentFromTime,
            wrapInternalComments: WrapStaffComments(appointment.InternalUserComments),
            rejectionNotes: null);

        await _dispatcher.DispatchAsync(
            templateCode: NotificationTemplateConsts.Codes.PatientAppointmentApprovedInternal,
            recipients: internalRecipients,
            variables: internalVars,
            contextTag: $"StatusChange/Approved/Responsible/{eventData.AppointmentId}");
    }

    private async Task DispatchRejectedAsync(
        AppointmentStatusChangedEto eventData,
        DocumentEmailContext ctx,
        Appointment appointment,
        string appointmentDate,
        string appointmentFromTime,
        List<NotificationRecipient> stakeholders)
    {
        if (stakeholders.Count == 0)
        {
            _logger.LogInformation(
                "StatusChangeEmailHandler: no stakeholders for Rejected appointment {AppointmentId}; skipping.",
                eventData.AppointmentId);
            return;
        }

        // OLD :984-991 (rejection). RejectionNotes comes from the
        // appointment row -- AppointmentApprovalAppService.RejectAppointmentAsync
        // persists it before publishing AppointmentStatusChangedEto.
        var rejectionNotes = appointment.RejectionNotes ?? eventData.Reason ?? string.Empty;

        var rejectVars = BuildVariables(
            ctx,
            appointment,
            appointmentDate,
            appointmentFromTime,
            wrapInternalComments: string.Empty,
            rejectionNotes: rejectionNotes);

        await _dispatcher.DispatchAsync(
            templateCode: NotificationTemplateConsts.Codes.PatientAppointmentRejected,
            recipients: stakeholders,
            variables: rejectVars,
            contextTag: $"StatusChange/Rejected/Stakeholders/{eventData.AppointmentId}");
    }

    /// <summary>
    /// Phase 2.C / Decision 4 (2026-05-08): CheckedIn fires
    /// <c>PatientAppointmentCheckedIn</c> to all stakeholders. OLD ::997-1002
    /// wraps the appointment's <c>RejectionNotes</c> column with the
    /// "Please note rejection reason:" prefix and surfaces it inside the
    /// CheckedIn body -- a clear OLD bug because a checked-in appointment
    /// has no rejection. NEW skips the RejectionNotes substitution entirely;
    /// the simplified body does not reference the token. NO CC (OLD :1002
    /// is the 3-arg overload).
    /// </summary>
    private async Task DispatchCheckedInAsync(
        AppointmentStatusChangedEto eventData,
        DocumentEmailContext ctx,
        Appointment appointment,
        string appointmentDate,
        string appointmentFromTime,
        List<NotificationRecipient> stakeholders)
    {
        if (stakeholders.Count == 0)
        {
            _logger.LogInformation(
                "StatusChangeEmailHandler: no stakeholders for CheckedIn appointment {AppointmentId}; skipping.",
                eventData.AppointmentId);
            return;
        }

        var vars = BuildVariables(
            ctx,
            appointment,
            appointmentDate,
            appointmentFromTime,
            wrapInternalComments: string.Empty,
            rejectionNotes: null);

        await _dispatcher.DispatchAsync(
            templateCode: NotificationTemplateConsts.Codes.PatientAppointmentCheckedIn,
            recipients: stakeholders,
            variables: vars,
            contextTag: $"StatusChange/CheckedIn/Stakeholders/{eventData.AppointmentId}");
    }

    /// <summary>
    /// Phase 2.C / Decision 4 (2026-05-08): CheckedOut fires
    /// <c>PatientAppointmentCheckedOut</c> to all stakeholders. Same
    /// RejectionNotes-skip as CheckedIn. OLD :1004-1014. NO CC.
    /// </summary>
    private async Task DispatchCheckedOutAsync(
        AppointmentStatusChangedEto eventData,
        DocumentEmailContext ctx,
        Appointment appointment,
        string appointmentDate,
        string appointmentFromTime,
        List<NotificationRecipient> stakeholders)
    {
        if (stakeholders.Count == 0)
        {
            _logger.LogInformation(
                "StatusChangeEmailHandler: no stakeholders for CheckedOut appointment {AppointmentId}; skipping.",
                eventData.AppointmentId);
            return;
        }

        var vars = BuildVariables(
            ctx,
            appointment,
            appointmentDate,
            appointmentFromTime,
            wrapInternalComments: string.Empty,
            rejectionNotes: null);

        await _dispatcher.DispatchAsync(
            templateCode: NotificationTemplateConsts.Codes.PatientAppointmentCheckedOut,
            recipients: stakeholders,
            variables: vars,
            contextTag: $"StatusChange/CheckedOut/Stakeholders/{eventData.AppointmentId}");
    }

    /// <summary>
    /// Phase 2.C / Decision 5 (2026-05-08): NoShow fires
    /// <c>PatientAppointmentNoShow</c> to internal staff only -- patient
    /// does NOT receive a no-show notification. Replicates OLD :1016-1026
    /// exactly: <c>emailTos</c> is rebuilt from a vInternalUserEmail walk
    /// limited to <c>StaffSupervisor + ClinicStaff</c>. NO CC (OLD :1026
    /// is the 3-arg overload).
    /// </summary>
    private async Task DispatchNoShowAsync(
        AppointmentStatusChangedEto eventData,
        DocumentEmailContext ctx,
        Appointment appointment,
        string appointmentDate,
        string appointmentFromTime)
    {
        var staffRecipients = await ResolveNoShowInternalRecipientsAsync(eventData.AppointmentId);
        if (staffRecipients.Count == 0)
        {
            _logger.LogInformation(
                "StatusChangeEmailHandler: no Staff Supervisor / Clinic Staff users in tenant; skipping NoShow for appointment {AppointmentId}.",
                eventData.AppointmentId);
            return;
        }

        var vars = BuildVariables(
            ctx,
            appointment,
            appointmentDate,
            appointmentFromTime,
            wrapInternalComments: string.Empty,
            rejectionNotes: null);

        await _dispatcher.DispatchAsync(
            templateCode: NotificationTemplateConsts.Codes.PatientAppointmentNoShow,
            recipients: staffRecipients,
            variables: vars,
            contextTag: $"StatusChange/NoShow/InternalStaff/{eventData.AppointmentId}");
    }

    /// <summary>
    /// Phase 2.C (2026-05-08): CancelledNoBill fires
    /// <c>PatientAppointmentCancelledNoBill</c> to all stakeholders. The
    /// body references the appointment's <c>CancellationReason</c> column
    /// -- the only one of the four new templates that surfaces a non-default
    /// variable. OLD :1029-1033. NO CC.
    /// </summary>
    private async Task DispatchCancelledNoBillAsync(
        AppointmentStatusChangedEto eventData,
        DocumentEmailContext ctx,
        Appointment appointment,
        string appointmentDate,
        string appointmentFromTime,
        List<NotificationRecipient> stakeholders)
    {
        if (stakeholders.Count == 0)
        {
            _logger.LogInformation(
                "StatusChangeEmailHandler: no stakeholders for CancelledNoBill appointment {AppointmentId}; skipping.",
                eventData.AppointmentId);
            return;
        }

        var baseVars = BuildVariables(
            ctx,
            appointment,
            appointmentDate,
            appointmentFromTime,
            wrapInternalComments: string.Empty,
            rejectionNotes: null);

        // Inject the appointment-specific cancellation reason on top of
        // the base bag. BuildVariables doesn't know about CancellationReason;
        // it's only referenced by this single template.
        var vars = new Dictionary<string, object?>(baseVars, StringComparer.Ordinal)
        {
            ["CancellationReason"] = appointment.CancellationReason ?? string.Empty,
        };

        await _dispatcher.DispatchAsync(
            templateCode: NotificationTemplateConsts.Codes.PatientAppointmentCancelledNoBill,
            recipients: stakeholders,
            variables: vars,
            contextTag: $"StatusChange/CancelledNoBill/Stakeholders/{eventData.AppointmentId}");
    }

    /// <summary>
    /// Phase 2.C: walks <see cref="IdentityUserManager"/> for every user in
    /// <c>Staff Supervisor</c> + <c>Clinic Staff</c> and packages them into
    /// <see cref="NotificationRecipient"/> rows. Mirrors OLD :1021's
    /// <c>vInternalUserEmail.RoleId == StaffSupervisor || ClinicStaff</c>
    /// query. Dedupes on email so a user with both roles only gets one
    /// email.
    /// </summary>
    private async Task<List<NotificationRecipient>> ResolveNoShowInternalRecipientsAsync(Guid appointmentId)
    {
        var byEmail = new Dictionary<string, NotificationRecipient>(StringComparer.OrdinalIgnoreCase);
        foreach (var roleName in NoShowInternalRoles)
        {
            var users = await _userManager.GetUsersInRoleAsync(roleName);
            foreach (var user in users)
            {
                if (string.IsNullOrWhiteSpace(user.Email))
                {
                    _logger.LogDebug(
                        "StatusChangeEmailHandler: skipping {Role} user {UserId} -- empty email; appointment {AppointmentId}.",
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
    /// Builds the variable bag the OLD-verbatim HTML bodies expect. Starts
    /// from <see cref="DocumentNotificationContext.BuildVariables"/> for
    /// the shared keys, then overrides <c>AppointmentDate</c> with OLD's
    /// dash-separated format and adds the status-email-specific keys
    /// (<c>AppointmentFromTime</c>, <c>InternalUserComments</c>,
    /// <c>RejectionNotes</c>) plus brand-token placeholders that the
    /// templates reference but per-tenant branding does not yet populate.
    /// </summary>
    private static IReadOnlyDictionary<string, object?> BuildVariables(
        DocumentEmailContext ctx,
        Appointment appointment,
        string appointmentDate,
        string appointmentFromTime,
        string wrapInternalComments,
        string? rejectionNotes)
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
            rejectionNotes: rejectionNotes,
            clinicName: null,
            portalUrl: ctx.PortalBaseUrl);

        var vars = new Dictionary<string, object?>(baseVars, StringComparer.Ordinal)
        {
            // OLD-format date overrides BuildVariables's MM/dd/yyyy.
            ["AppointmentDate"] = appointmentDate,
            ["AppointmentFromTime"] = appointmentFromTime,
            ["InternalUserComments"] = wrapInternalComments ?? string.Empty,
        };

        AddBrandPlaceholders(vars);
        return vars;
    }

    /// <summary>
    /// Per-tenant branding tokens the OLD HTML templates reference. Until
    /// per-tenant branding ships, every brand token resolves to empty
    /// string. Tracked in docs/parity/_parity-flags.md as
    /// "Email branding tokens unbranded".
    /// </summary>
    private static void AddBrandPlaceholders(Dictionary<string, object?> vars)
    {
        // The tokens are referenced literally (with the ##...## wrapping
        // stripped) by the substitutor, so the dictionary key is the
        // bare name.
        vars["CompanyLogo"] = string.Empty;
        vars["lblHeaderTitle"] = string.Empty;
        vars["lblFooterText"] = string.Empty;
        vars["Email"] = string.Empty;
        vars["Skype"] = string.Empty;
        vars["ph_US"] = string.Empty;
        vars["fax"] = string.Empty;
        vars["imageInByte"] = string.Empty;
    }

    private static NotificationKind MapKind(AppointmentStatusType status) => status switch
    {
        AppointmentStatusType.Approved => NotificationKind.Approved,
        AppointmentStatusType.Rejected => NotificationKind.Rejected,
        // Phase 2.C (2026-05-08): the four deferred status emails reuse
        // the Approved fan-out (same Patient + AA + DA + CE + Office set
        // OLD already mailed via emailTos at AppointmentDomain.cs:910/990).
        // The resolver doesn't gate behavior by kind today; using Approved
        // here is purely a context tag for downstream logging.
        AppointmentStatusType.CheckedIn => NotificationKind.Approved,
        AppointmentStatusType.CheckedOut => NotificationKind.Approved,
        AppointmentStatusType.CancelledNoBill => NotificationKind.Approved,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    private static string FormatTimeOnlyOrEmpty(TimeOnly? time)
    {
        if (!time.HasValue)
        {
            return string.Empty;
        }
        // OLD format: hh:mm tt (12h with AM/PM, en-US invariant).
        // (AppointmentDocumentDomain.cs:913.)
        return time.Value.ToString("hh:mm tt", CultureInfo.GetCultureInfo("en-US"));
    }

    /// <summary>
    /// OLD :976-977 -- "Please note:" prefix on the stakeholder template.
    /// Empty when the comments field is empty (matches OLD's
    /// <c>String.IsNullOrEmpty</c> guard at line 974).
    /// </summary>
    private static string WrapPleaseNote(string? raw)
    {
        return string.IsNullOrWhiteSpace(raw)
            ? string.Empty
            : $"<b> Please note: </b>{raw}";
    }

    /// <summary>
    /// OLD :962-963 -- "Staff comments for an appointment:" prefix on the
    /// responsible-user template.
    /// </summary>
    private static string WrapStaffComments(string? raw)
    {
        return string.IsNullOrWhiteSpace(raw)
            ? string.Empty
            : $"<b> Staff comments for an appointment: </b>{raw}";
    }
}
