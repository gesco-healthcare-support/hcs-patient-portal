using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Local;
using HealthcareSupport.CaseEvaluation.Notifications;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using Microsoft.Extensions.Logging;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

/// <summary>
/// Phase 15 (2026-05-04) -- external-user cancel submit. AppService
/// composes the per-row edit-access policy
/// (<see cref="AppointmentAccessRules.CanEdit"/>) with the domain
/// service's
/// <see cref="AppointmentChangeRequestManager.SubmitCancellationAsync"/>
/// orchestrator.
/// </summary>
[RemoteService(IsEnabled = false)]
[Authorize]
public class AppointmentChangeRequestsAppService : CaseEvaluationAppService, IAppointmentChangeRequestsAppService
{
    private readonly AppointmentChangeRequestManager _manager;
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly AppointmentReadAccessGuard _readAccessGuard;
    // Phase 16 (2026-05-04) -- lead-time + per-AppointmentType max-time
    // gates reuse the booking-flow validator. The slot lookup happens
    // here so we can resolve the slot's AvailableDate before the
    // policy gate fires.
    private readonly BookingPolicyValidator _bookingPolicyValidator;
    private readonly IRepository<HealthcareSupport.CaseEvaluation.DoctorAvailabilities.DoctorAvailability, Guid> _doctorAvailabilityRepository;
    // Group D (2026-06-09): opposing-side consent issuance + notification.
    private readonly ChangeRequestConsentManager _consentManager;
    private readonly ChangeRequestSideResolver _sideResolver;
    private readonly IAccountUrlBuilder _accountUrlBuilder;
    private readonly ILocalEventBus _localEventBus;
    private readonly IAppointmentChangeRequestRepository _changeRequestRepository;

    public AppointmentChangeRequestsAppService(
        AppointmentChangeRequestManager manager,
        IAppointmentRepository appointmentRepository,
        AppointmentReadAccessGuard readAccessGuard,
        BookingPolicyValidator bookingPolicyValidator,
        IRepository<HealthcareSupport.CaseEvaluation.DoctorAvailabilities.DoctorAvailability, Guid> doctorAvailabilityRepository,
        ChangeRequestConsentManager consentManager,
        ChangeRequestSideResolver sideResolver,
        IAccountUrlBuilder accountUrlBuilder,
        ILocalEventBus localEventBus,
        IAppointmentChangeRequestRepository changeRequestRepository)
    {
        _manager = manager;
        _appointmentRepository = appointmentRepository;
        _readAccessGuard = readAccessGuard;
        _bookingPolicyValidator = bookingPolicyValidator;
        _doctorAvailabilityRepository = doctorAvailabilityRepository;
        _consentManager = consentManager;
        _sideResolver = sideResolver;
        _accountUrlBuilder = accountUrlBuilder;
        _localEventBus = localEventBus;
        _changeRequestRepository = changeRequestRepository;
    }

    [Authorize]
    public virtual async Task<AppointmentChangeRequestDto> RequestCancellationAsync(
        Guid appointmentId,
        RequestCancellationDto input)
    {
        if (appointmentId == Guid.Empty)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["Appointment"]]);
        }
        if (input == null || string.IsNullOrWhiteSpace(input.Reason))
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["CancellationReason"]]);
        }

        // Per-row edit-access policy. Internal users (admin / Clinic
        // Staff / Staff Supervisor / IT Admin / Doctor) bypass; external
        // users must be the creator OR hold an accessor row with
        // AccessType.Edit. View accessors are rejected.
        await EnsureCanEditAsync(appointmentId);

        // B1 (2026-07-01): internal staff may cancel a not-yet-approved
        // (Pending) appointment; external users stay Approved-only.
        var allowPendingSource = BookingFlowRoles.IsInternalUserCaller(CurrentUser.Roles);

        var changeRequest = await _manager.SubmitCancellationAsync(
            appointmentId: appointmentId,
            cancellationReason: input.Reason,
            allowPendingSource: allowPendingSource,
            actingUserId: CurrentUser.Id);

        await IssueConsentAndNotifyAsync(changeRequest);

        return ObjectMapper.Map<AppointmentChangeRequest, AppointmentChangeRequestDto>(changeRequest);
    }

    [Authorize]
    public virtual async Task<AppointmentChangeRequestDto> RequestRescheduleAsync(
        Guid appointmentId,
        RequestRescheduleDto input)
    {
        if (appointmentId == Guid.Empty)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["Appointment"]]);
        }
        if (input == null || string.IsNullOrWhiteSpace(input.ReScheduleReason))
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["ReScheduleReason"]]);
        }

        // Per-row edit-access policy -- same as cancellation submit.
        await EnsureCanEditAsync(appointmentId);

        // Look up the appointment so we can run the booking-policy
        // gates against its AppointmentTypeId (lead-time + per-type
        // max-time). Per OLD parity these gates are identical to the
        // booking flow's gates.
        var appointment = await _appointmentRepository.FindAsync(appointmentId);
        if (appointment == null)
        {
            throw new EntityNotFoundException(typeof(Appointment), appointmentId);
        }

        // Resolve the new slot's date so the booking policy validator
        // can reason about it. The validator throws BusinessException
        // with the same lead-time / max-horizon codes used by the
        // booking flow on failure (parity-preserved).
        var newSlot = await _doctorAvailabilityRepository.FindAsync(input.NewDoctorAvailabilityId);
        if (newSlot == null)
        {
            throw new EntityNotFoundException(
                typeof(HealthcareSupport.CaseEvaluation.DoctorAvailabilities.DoctorAvailability),
                input.NewDoctorAvailabilityId);
        }
        // 2026-06-11 -- role-aware horizon, same as the booking flow: a
        // reschedule re-picks a slot date, so an external requester is bound
        // by the per-type horizon (60) and internal staff by the internal
        // horizon (90). Without this, an external user could reschedule past
        // the 60-day window the create flow enforces.
        var isInternalRescheduler = BookingFlowRoles.IsInternalUserCaller(CurrentUser.Roles);
        await _bookingPolicyValidator.ValidateAsync(newSlot.AvailableDate, appointment.AppointmentTypeId, isInternalRescheduler);

        // B1 (2026-07-01): the same internal-caller flag admits a Pending
        // source appointment for the reschedule request; external stays
        // Approved-only.
        var changeRequest = await _manager.SubmitRescheduleAsync(
            appointmentId: appointmentId,
            newDoctorAvailabilityId: input.NewDoctorAvailabilityId,
            reScheduleReason: input.ReScheduleReason,
            isBeyondLimit: input.IsBeyondLimit,
            allowPendingSource: isInternalRescheduler,
            actingUserId: CurrentUser.Id);

        await IssueConsentAndNotifyAsync(changeRequest);

        return ObjectMapper.Map<AppointmentChangeRequest, AppointmentChangeRequestDto>(changeRequest);
    }

    private async Task EnsureCanEditAsync(Guid appointmentId)
    {
        // F-013 fix (2026-06-23): use the change-request access rule (booker + every named
        // party + Edit-accessor) instead of the slim creator/Edit-accessor rule, which 403'd
        // the named attorney-of-record + patient on paralegal-booked appointments. Keep this
        // flow's own error code so the change-request contract is unchanged.
        if (!await _readAccessGuard.CanRequestChangeAsync(appointmentId))
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.ChangeRequestEditAccessRequired)
                .WithData("appointmentId", appointmentId);
        }
    }

    /// <summary>
    /// Issues consent on a just-submitted request (reschedule/cancel consent redesign,
    /// 2026-07-01). Party-initiated (external submitter maps to a side): auto-grant the
    /// requestor's side and token the OPPOSING side (one Yes/No email). Staff-initiated
    /// (internal caller, no side): token BOTH sides that have a representative (one email
    /// each); a side with no rep is left NotRequired (auto-satisfied). No-op when gating is
    /// off. If an external submitter's side cannot be resolved (defensive), consent stays
    /// NotRequired so the Staff Supervisor mediates.
    /// </summary>
    private async Task IssueConsentAndNotifyAsync(AppointmentChangeRequest changeRequest)
    {
        if (!AppointmentChangeRequestConsts.ConsentGatingEnabled || !changeRequest.TenantId.HasValue)
        {
            return;
        }

        var submitterId = CurrentUser.Id ?? Guid.Empty;

        if (!BookingFlowRoles.IsInternalUserCaller(CurrentUser.Roles))
        {
            // Party-initiated: the requestor's side is implied (auto-granted); the opposing
            // side must consent. F-014: the resolver places a booker/paralegal on a side too.
            var resolution = await _sideResolver.ResolveAsync(
                changeRequest.AppointmentId, CurrentUser.Email, CurrentUser.Roles);
            if (resolution == null)
            {
                Logger.LogWarning(
                    "ChangeRequest {ChangeRequestId}: submitter side unresolved; consent skipped (Staff Supervisor mediates).",
                    changeRequest.Id);
                return;
            }

            changeRequest.InitiateConsent(resolution.RequestingSide, submitterId);
            changeRequest.AutoGrantSide(resolution.RequestingSide, Clock.Now.ToUniversalTime());

            var opposingSide = resolution.RequestingSide == ChangeRequestSide.SideA
                ? ChangeRequestSide.SideB
                : ChangeRequestSide.SideA;
            var rawToken = _consentManager.IssueSideConsent(changeRequest, opposingSide);
            await _changeRequestRepository.UpdateAsync(changeRequest, autoSave: true);

            await PublishConsentRequestedAsync(
                changeRequest, resolution.OpposingRepEmail, resolution.OpposingRepRole, rawToken);
            return;
        }

        // Staff-initiated: neither side is the requestor, so BOTH sides that have a rep must
        // consent before a supervisor can finalize. A side with no rep stays NotRequired.
        changeRequest.InitiateConsent(null, submitterId);
        var bothSides = await _sideResolver.ResolveBothSidesAsync(changeRequest.AppointmentId);

        var toNotify = new List<(string Email, RecipientRole Role, string Token)>();
        if (!string.IsNullOrWhiteSpace(bothSides.SideARepEmail))
        {
            var token = _consentManager.IssueSideConsent(changeRequest, ChangeRequestSide.SideA);
            toNotify.Add((bothSides.SideARepEmail!, bothSides.SideARepRole ?? RecipientRole.Patient, token));
        }
        if (!string.IsNullOrWhiteSpace(bothSides.SideBRepEmail))
        {
            var token = _consentManager.IssueSideConsent(changeRequest, ChangeRequestSide.SideB);
            toNotify.Add((bothSides.SideBRepEmail!, bothSides.SideBRepRole ?? RecipientRole.ClaimExaminer, token));
        }

        if (toNotify.Count == 0)
        {
            Logger.LogWarning(
                "ChangeRequest {ChangeRequestId}: staff-initiated but neither side has a representative; consent skipped.",
                changeRequest.Id);
            return;
        }

        await _changeRequestRepository.UpdateAsync(changeRequest, autoSave: true);

        foreach (var (email, role, token) in toNotify)
        {
            await PublishConsentRequestedAsync(changeRequest, email, role, token);
        }
    }

    private async Task PublishConsentRequestedAsync(
        AppointmentChangeRequest changeRequest,
        string recipientEmail,
        RecipientRole recipientRole,
        string rawToken)
    {
        var consentUrl = await _accountUrlBuilder.BuildChangeRequestConsentUrlAsync(
            changeRequest.TenantId!.Value, rawToken);

        await _localEventBus.PublishAsync(new ChangeRequestConsentRequestedEto
        {
            AppointmentId = changeRequest.AppointmentId,
            ChangeRequestId = changeRequest.Id,
            TenantId = changeRequest.TenantId,
            ChangeRequestType = changeRequest.ChangeRequestType,
            OpposingRecipientEmail = recipientEmail,
            OpposingRecipientRole = recipientRole,
            ConsentUrl = consentUrl,
            OccurredAt = DateTime.UtcNow,
        });
    }
}
