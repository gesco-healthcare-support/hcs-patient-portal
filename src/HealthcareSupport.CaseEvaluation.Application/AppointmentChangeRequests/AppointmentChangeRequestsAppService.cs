using HealthcareSupport.CaseEvaluation.Appointments;
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

        var changeRequest = await _manager.SubmitCancellationAsync(
            appointmentId: appointmentId,
            cancellationReason: input.Reason,
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
        await _bookingPolicyValidator.ValidateAsync(newSlot.AvailableDate, appointment.AppointmentTypeId);

        var changeRequest = await _manager.SubmitRescheduleAsync(
            appointmentId: appointmentId,
            newDoctorAvailabilityId: input.NewDoctorAvailabilityId,
            reScheduleReason: input.ReScheduleReason,
            isBeyondLimit: input.IsBeyondLimit,
            actingUserId: CurrentUser.Id);

        await IssueConsentAndNotifyAsync(changeRequest);

        return ObjectMapper.Map<AppointmentChangeRequest, AppointmentChangeRequestDto>(changeRequest);
    }

    private async Task EnsureCanEditAsync(Guid appointmentId)
    {
        // Same slim edit rule (internal / creator / Edit-accessor) as before, now
        // centralised in AppointmentReadAccessGuard.CanEditAsync. Keep this flow's own
        // error code so the change-request contract is unchanged.
        if (!await _readAccessGuard.CanEditAsync(appointmentId))
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.ChangeRequestEditAccessRequired)
                .WithData("appointmentId", appointmentId);
        }
    }

    /// <summary>
    /// Group D (2026-06-09): issue the opposing-side consent token on the just-submitted
    /// request and publish <see cref="ChangeRequestConsentRequestedEto"/> so the actionable
    /// Yes/No email goes to the opposing side's representative. No-op when consent gating is
    /// off; when the opposing side cannot be resolved (defensive), consent stays NotRequired
    /// so the Staff Supervisor can finalize directly.
    /// </summary>
    private async Task IssueConsentAndNotifyAsync(AppointmentChangeRequest changeRequest)
    {
        if (!AppointmentChangeRequestConsts.ConsentGatingEnabled || !changeRequest.TenantId.HasValue)
        {
            return;
        }

        var resolution = await _sideResolver.ResolveAsync(changeRequest.AppointmentId, CurrentUser.Email);
        if (resolution == null)
        {
            Logger.LogWarning(
                "ChangeRequest {ChangeRequestId}: opposing side unresolved; consent skipped (Staff Supervisor finalizes directly).",
                changeRequest.Id);
            return;
        }

        var rawToken = _consentManager.IssueConsent(
            changeRequest, resolution.RequestingSide, CurrentUser.Id ?? Guid.Empty);
        await _changeRequestRepository.UpdateAsync(changeRequest, autoSave: true);

        var consentUrl = await _accountUrlBuilder.BuildChangeRequestConsentUrlAsync(
            changeRequest.TenantId.Value, rawToken);

        await _localEventBus.PublishAsync(new ChangeRequestConsentRequestedEto
        {
            AppointmentId = changeRequest.AppointmentId,
            ChangeRequestId = changeRequest.Id,
            TenantId = changeRequest.TenantId,
            ChangeRequestType = changeRequest.ChangeRequestType,
            OpposingRecipientEmail = resolution.OpposingRepEmail,
            OpposingRecipientRole = resolution.OpposingRepRole,
            ConsentUrl = consentUrl,
            OccurredAt = DateTime.UtcNow,
        });
    }
}
