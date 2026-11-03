using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;
using HealthcareSupport.CaseEvaluation.AppointmentInfoRequests;
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
///     stakeholder-side fan-out). OLD's second email, to the responsible
///     user (<c>PatientAppointmentApprovedInternal</c>, OLD :957-966), was
///     retired with the responsible user on 2026-11-02.</item>
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
    private readonly IRepository<AppointmentInfoRequest, Guid> _infoRequestRepository;
    private readonly IRepository<AppointmentChangeRequest, Guid> _changeRequestRepository;
    private readonly IRepository<DoctorAvailability, Guid> _doctorAvailabilityRepository;
    private readonly BookerCcDispatcher _bookerCcDispatcher;
    private readonly OfficeNotificationRecipients _officeRecipients;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<StatusChangeEmailHandler> _logger;

    public StatusChangeEmailHandler(
        INotificationDispatcher dispatcher,
        DocumentEmailContextResolver contextResolver,
        IAppointmentRecipientResolver recipientResolver,
        IRepository<Appointment, Guid> appointmentRepository,
        IRepository<AppointmentInfoRequest, Guid> infoRequestRepository,
        IRepository<AppointmentChangeRequest, Guid> changeRequestRepository,
        IRepository<DoctorAvailability, Guid> doctorAvailabilityRepository,
        BookerCcDispatcher bookerCcDispatcher,
        OfficeNotificationRecipients officeRecipients,
        ICurrentTenant currentTenant,
        ILogger<StatusChangeEmailHandler> logger)
    {
        _dispatcher = dispatcher;
        _contextResolver = contextResolver;
        _recipientResolver = recipientResolver;
        _appointmentRepository = appointmentRepository;
        _infoRequestRepository = infoRequestRepository;
        _changeRequestRepository = changeRequestRepository;
        _doctorAvailabilityRepository = doctorAvailabilityRepository;
        _bookerCcDispatcher = bookerCcDispatcher;
        _officeRecipients = officeRecipients;
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

        // 2026-11-03: an event whose status did not change is not a status change. The appointment
        // edit publishes one when only the SLOT moved (from == to == the current status), and without
        // this an Approved appointment that merely changed slot re-sent the Approved email to everyone.
        if (eventData.FromStatus == eventData.ToStatus)
        {
            return;
        }

        // 2026-11-03: turning down a reschedule request moves RescheduleRequested back to Approved. The
        // turned-down email is the notice for that; the Approved email must not fire again.
        if (eventData.ToStatus == AppointmentStatusType.Approved
            && eventData.FromStatus == AppointmentStatusType.RescheduleRequested)
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

            // NoShow goes to the office inbox + Intake Staff only (2026-11-02), so it
            // computes its own recipient set inside the dispatch method rather than via
            // the stakeholder resolver. The others (Approved, Rejected, CancelledNoBill,
            // InfoRequested) use the standard stakeholder fan-out.
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

                // 2026-11-03: rejected and sent-back are "before approval" emails. When someone other
                // than the patient submitted the request, the patient is told nothing until it is approved.
                if (status is AppointmentStatusType.Rejected or AppointmentStatusType.InfoRequested)
                {
                    stakeholders = PatientBeforeApprovalRule.Apply(stakeholders, ctx);
                }
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

                    case AppointmentStatusType.NoShow:
                        await DispatchNoShowAsync(
                            eventData, ctx, appointment, appointmentDate, appointmentFromTime);
                        break;

                    case AppointmentStatusType.CancelledNoBill:
                        // 2026-11-03: a cancellation finished from a change request already sends the
                        // change-request approved email to the same people, so this one is skipped.
                        if (await HasCancelChangeRequestAsync(eventData.AppointmentId))
                        {
                            _logger.LogInformation(
                                "StatusChangeEmailHandler: appointment {AppointmentId} was cancelled through a change "
                                + "request; the change-request email is the one notice, so the status email is skipped.",
                                eventData.AppointmentId);
                            break;
                        }

                        await DispatchCancelledNoBillAsync(
                            eventData, ctx, appointment, appointmentDate, appointmentFromTime, stakeholders);
                        break;

                    case AppointmentStatusType.InfoRequested:
                        await DispatchInfoRequestedAsync(
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
                    ex,
                    "StatusChangeEmailHandler: template missing for appointment {AppointmentId} status {Status}; email skipped. Detail: {Detail}",
                    eventData.AppointmentId,
                    eventData.ToStatus,
                    ex.Data.Contains("templateCode") ? ex.Data["templateCode"] : "(unspecified)");
            }
        }
    }

    /// <summary>
    /// True when the appointment has a cancellation change request that is open or accepted -- the
    /// state a request is in while its finalizer moves the appointment to a cancelled status. Every
    /// path to CancelledNoBill runs through one.
    /// </summary>
    private async Task<bool> HasCancelChangeRequestAsync(Guid appointmentId)
    {
        return await _changeRequestRepository.AnyAsync(cr => cr.AppointmentId == appointmentId
            && cr.ChangeRequestType == ChangeRequestType.Cancel
            && (cr.RequestStatus == RequestStatusType.Pending || cr.RequestStatus == RequestStatusType.Accepted));
    }

    /// <summary>
    /// Phase 2.C (2026-05-08): the statuses this handler covers. Any
    /// other status passes through silently.
    /// </summary>
    internal static bool IsHandledStatus(AppointmentStatusType? status) => status switch
    {
        AppointmentStatusType.Approved => true,
        AppointmentStatusType.Rejected => true,
        AppointmentStatusType.NoShow => true,
        AppointmentStatusType.CancelledNoBill => true,
        AppointmentStatusType.InfoRequested => true,
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
        if (stakeholders.Count > 0 || !string.IsNullOrWhiteSpace(ctx.BookerEmail))
        {
            var extVars = BuildVariables(
                ctx,
                appointment,
                appointmentDate,
                appointmentFromTime,
                wrapInternalComments: WrapPleaseNote(appointment.InternalUserComments),
                rejectionNotes: null);
            // C1 (2026-06-09): one message To the booker, CC the other parties
            // (was a per-recipient fan-out). The resolver already put the office inbox
            // among the stakeholders; 2026-11-02 adds the Intake Staff and drops the
            // per-tenant CcEmailIds list.
            stakeholders = new List<NotificationRecipient>(stakeholders);
            await _officeRecipients.AppendIntakeAsync(stakeholders);
            await DispatchToBookerWithCcAsync(
                templateCode: NotificationTemplateConsts.Codes.PatientAppointmentApprovedExt,
                ctx: ctx,
                stakeholders: stakeholders,
                variables: extVars,
                contextTag: $"StatusChange/Approved/Stakeholders/{eventData.AppointmentId}");
        }
        else
        {
            _logger.LogInformation(
                "StatusChangeEmailHandler: no stakeholders for Approved appointment {AppointmentId}; skipping ext send.",
                eventData.AppointmentId);
        }

        // OLD :953-967 sent a second email, PatientAppointmentApprovedInternal, to the appointment's
        // responsible user. The office retired the responsible user on 2026-11-02, so only the
        // stakeholder email above is sent; stored PrimaryResponsibleUserId values are ignored.
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

        // C3 (2026-06-09): one message To the booker, CC the other parties + office.
        // PARITY-FLAG: OLD rejection (AppointmentDomain.cs:990) sent with NO CC
        // (3-arg SendSMTPMail); NEW adds the office CC per Adrian 2026-06-09.
        // (OLD source: AppointmentDomain.cs:984-991) 2026-11-02: Intake Staff join the CC.
        stakeholders = new List<NotificationRecipient>(stakeholders);
        await _officeRecipients.AppendIntakeAsync(stakeholders);
        await DispatchToBookerWithCcAsync(
            templateCode: NotificationTemplateConsts.Codes.PatientAppointmentRejected,
            ctx: ctx,
            stakeholders: stakeholders,
            variables: rejectVars,
            contextTag: $"StatusChange/Rejected/Stakeholders/{eventData.AppointmentId}");
    }

    /// <summary>
    /// Phase 2.C / Decision 5 (2026-05-08): NoShow fires
    /// <c>PatientAppointmentNoShow</c> to internal staff only -- patient
    /// does NOT receive a no-show notification. OLD :1016-1026 mailed Staff
    /// Supervisor + Intake Staff; since 2026-11-02 the recipients are the office
    /// inbox + the switched-on Intake Staff (Supervisors get no per-event mail).
    /// NO CC (OLD :1026 is the 3-arg overload).
    /// </summary>
    private async Task DispatchNoShowAsync(
        AppointmentStatusChangedEto eventData,
        DocumentEmailContext ctx,
        Appointment appointment,
        string appointmentDate,
        string appointmentFromTime)
    {
        var staffRecipients = await _officeRecipients.GetOfficeInboxAndIntakeAsync(
            $"NoShow/{eventData.AppointmentId}");
        if (staffRecipients.Count == 0)
        {
            _logger.LogInformation(
                "StatusChangeEmailHandler: no office inbox or Intake Staff in tenant; skipping NoShow for appointment {AppointmentId}.",
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

        // C7 (2026-06-09): one message To the booker, CC the other parties + office.
        // PARITY-FLAG: OLD CancelledNoBill (AppointmentDomain.cs:1033) sent with
        // NO CC; NEW adds the office CC per Adrian 2026-06-09. (OLD source: :1029-1033)
        await DispatchToBookerWithCcAsync(
            templateCode: NotificationTemplateConsts.Codes.PatientAppointmentCancelledNoBill,
            ctx: ctx,
            stakeholders: stakeholders,
            variables: vars,
            contextTag: $"StatusChange/CancelledNoBill/Stakeholders/{eventData.AppointmentId}");
    }

    /// <summary>
    /// Prompt 17 (2026-06-17): InfoRequested fires
    /// <c>PatientAppointmentInfoRequested</c> To the REQUESTER (booker);
    /// since 2026-11-02 the other parties and the office inbox are CC'd (not
    /// Intake Staff). The body carries the staff note + a deep
    /// link to the external fix-it page and templates NO field values (HIPAA:
    /// the requester sees the specific fields on the page, not in the email).
    /// </summary>
    private async Task DispatchInfoRequestedAsync(
        AppointmentStatusChangedEto eventData,
        DocumentEmailContext ctx,
        Appointment appointment,
        string appointmentDate,
        string appointmentFromTime,
        List<NotificationRecipient> stakeholders)
    {
        // 2026-11-03: the requester is the submitter. A staff submitter is not mailed, so the first
        // remaining party (never the patient before approval, never the office) is the To instead.
        var requesterEmail = ctx.PrimaryRecipientEmail ?? ctx.BookerEmail
            ?? stakeholders.FirstOrDefault(r => (r.Role ?? RecipientRole.Patient) != RecipientRole.OfficeAdmin)?.Email;
        if (string.IsNullOrWhiteSpace(requesterEmail))
        {
            _logger.LogWarning(
                "StatusChangeEmailHandler: no requester email for InfoRequested appointment {AppointmentId}; skipping.",
                eventData.AppointmentId);
            return;
        }

        var openRequest = await _infoRequestRepository.FirstOrDefaultAsync(
            r => r.AppointmentId == eventData.AppointmentId
                && r.Status == InfoRequestStatus.Open);

        var baseVars = BuildVariables(
            ctx,
            appointment,
            appointmentDate,
            appointmentFromTime,
            wrapInternalComments: string.Empty,
            rejectionNotes: null);

        var vars = new Dictionary<string, object?>(baseVars, StringComparer.Ordinal)
        {
            ["InfoRequestNote"] = openRequest?.Note ?? string.Empty,
            ["AppointmentViewUrl"] = BuildFixItUrl(ctx.PortalBaseUrl, eventData.AppointmentId),
        };

        // The resolver's party set already carries the office inbox; the dispatcher
        // drops a CC equal to the To address and repeats within the CC list.
        await _dispatcher.DispatchToWithCcAsync(
            templateCode: NotificationTemplateConsts.Codes.PatientAppointmentInfoRequested,
            to: new NotificationRecipient(email: requesterEmail, isRegistered: true),
            cc: stakeholders,
            variables: vars,
            // Send Back can happen more than once for one appointment (Pending -> InfoRequested ->
            // resubmitted -> InfoRequested again), so each one is its own email.
            contextTag: NotificationOccurrence.At(
                $"StatusChange/InfoRequested/Requester/{eventData.AppointmentId}", eventData.OccurredAt));
    }

    /// <summary>
    /// Composes the absolute external fix-it URL (<c>/appointments/view/{id}</c>)
    /// from the per-tenant portal base URL. The route is the role-split external
    /// appointment detail, which renders the fix-it flow while InfoRequested.
    /// </summary>
    private static string BuildFixItUrl(string? portalBaseUrl, Guid appointmentId)
    {
        var baseUrl = (portalBaseUrl ?? string.Empty).TrimEnd('/');
        return $"{baseUrl}/appointments/view/{appointmentId}";
    }

    /// <summary>
    /// C-group (2026-06-09): sends ONE message addressed To the booker with the
    /// other parties CC'd, via
    /// <see cref="INotificationDispatcher.DispatchToWithCcAsync"/>. The booker is
    /// resolved from the appointment creator (<c>ctx.BookerEmail</c>); when the
    /// booker is not among the resolved parties (e.g. a staff-created booking)
    /// the booker is added as the To recipient and every party is CC'd.
    /// Replaces the prior per-recipient fan-out, which sent separate copies of
    /// the same notice to each party (effectively ex-parte).
    /// </summary>
    private Task DispatchToBookerWithCcAsync(
        string templateCode,
        DocumentEmailContext ctx,
        List<NotificationRecipient> stakeholders,
        IReadOnlyDictionary<string, object?> variables,
        string contextTag)
    {
        // Phase 4 (C3/D3): when the booker is a promoted attorney-creator
        // (firm/paralegal), address To the named attorney and make sure the
        // firm/paralegal creator is CC'd -- they are often NOT otherwise a
        // resolved party (they entered a different attorney email, and when the
        // patient has a login appointment.IdentityUserId = patient). Scoped to the
        // promoted case so Patient / Claim Examiner / internal-staff bookings are
        // unchanged. BookerCcDispatcher dedups a CC equal to the To, so a solo
        // attorney (To == creator == attorney) stays a single recipient.
        var recipients = stakeholders;
        if (ctx.IsPromoted && !string.IsNullOrWhiteSpace(ctx.CreatorEmail))
        {
            recipients = new List<NotificationRecipient>(stakeholders)
            {
                // Role is CC metadata only (To/CC split is by email); OfficeAdmin
                // mirrors how the codebase tags appended non-party CC recipients.
                new(email: ctx.CreatorEmail, role: RecipientRole.OfficeAdmin, isRegistered: true),
            };
        }

        // Group F (2026-06-09): the To-booker/CC partition, office-CC append,
        // and single-message dispatch were extracted to the shared
        // BookerCcDispatcher so the consolidated reminder reuses the same rule.
        return _bookerCcDispatcher.DispatchToBookerWithCcAsync(
            templateCode: templateCode,
            bookerEmail: ctx.PrimaryRecipientEmail ?? ctx.BookerEmail,
            stakeholders: recipients,
            variables: variables,
            contextTag: contextTag);
    }

    private static string JoinName(string? first, string? last)
    {
        var hasFirst = !string.IsNullOrWhiteSpace(first);
        var hasLast = !string.IsNullOrWhiteSpace(last);
        if (hasFirst && hasLast) return first!.Trim() + " " + last!.Trim();
        if (hasFirst) return first!.Trim();
        if (hasLast) return last!.Trim();
        return string.Empty;
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
    private static Dictionary<string, object?> BuildVariables(
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

        // Phase 4 (D5): when promoted, greet the named attorney (ctx.GreetingName);
        // otherwise greet the booker, falling back to the patient name. The
        // template token stays "BookerFullName" -- only the value it carries
        // changes for the promoted (attorney-booker) case.
        string bookerName;
        if (!string.IsNullOrWhiteSpace(ctx.GreetingName))
        {
            bookerName = ctx.GreetingName;
        }
        else if (!string.IsNullOrWhiteSpace(ctx.BookerFullName))
        {
            bookerName = ctx.BookerFullName;
        }
        else
        {
            bookerName = JoinName(ctx.PatientFirstName, ctx.PatientLastName);
        }

        var vars = new Dictionary<string, object?>(baseVars, StringComparer.Ordinal)
        {
            // OLD-format date overrides BuildVariables's MM/dd/yyyy.
            ["AppointmentDate"] = appointmentDate,
            ["AppointmentFromTime"] = appointmentFromTime,
            ["InternalUserComments"] = wrapInternalComments ?? string.Empty,
            // C-group (2026-06-09): templates greet the booker (the appointment
            // creator, ctx.BookerFullName) -- or the promoted attorney (Phase 4).
            // Falls back to the patient name when neither has a display name.
            ["BookerFullName"] = bookerName,
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
        AppointmentStatusType.CancelledNoBill => NotificationKind.Approved,
        AppointmentStatusType.InfoRequested => NotificationKind.Approved,
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
}
