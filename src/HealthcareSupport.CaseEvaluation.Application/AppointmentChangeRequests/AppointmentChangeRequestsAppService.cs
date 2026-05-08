using HealthcareSupport.CaseEvaluation.AppointmentAccessors;
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
    private readonly IRepository<AppointmentAccessor, Guid> _appointmentAccessorRepository;
    // Phase 16 (2026-05-04) -- lead-time + per-AppointmentType max-time
    // gates reuse the booking-flow validator. The slot lookup happens
    // here so we can resolve the slot's AvailableDate before the
    // policy gate fires.
    private readonly BookingPolicyValidator _bookingPolicyValidator;
    private readonly IRepository<HealthcareSupport.CaseEvaluation.DoctorAvailabilities.DoctorAvailability, Guid> _doctorAvailabilityRepository;

    public AppointmentChangeRequestsAppService(
        AppointmentChangeRequestManager manager,
        IAppointmentRepository appointmentRepository,
        IRepository<AppointmentAccessor, Guid> appointmentAccessorRepository,
        BookingPolicyValidator bookingPolicyValidator,
        IRepository<HealthcareSupport.CaseEvaluation.DoctorAvailabilities.DoctorAvailability, Guid> doctorAvailabilityRepository)
    {
        _manager = manager;
        _appointmentRepository = appointmentRepository;
        _appointmentAccessorRepository = appointmentAccessorRepository;
        _bookingPolicyValidator = bookingPolicyValidator;
        _doctorAvailabilityRepository = doctorAvailabilityRepository;
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
        if (input == null)
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

        return ObjectMapper.Map<AppointmentChangeRequest, AppointmentChangeRequestDto>(changeRequest);
    }

    private async Task EnsureCanEditAsync(Guid appointmentId)
    {
        var appointment = await _appointmentRepository.FindAsync(appointmentId);
        if (appointment == null)
        {
            throw new EntityNotFoundException(typeof(Appointment), appointmentId);
        }

        var callerRoles = CurrentUser.Roles ?? Array.Empty<string>();
        var isInternal = BookingFlowRoles.IsInternalUserCaller(callerRoles);

        var accessorQuery = await _appointmentAccessorRepository.GetQueryableAsync();
        var entries = await AsyncExecuter.ToListAsync(
            accessorQuery
                .Where(a => a.AppointmentId == appointmentId)
                .Select(a => new AppointmentAccessRules.AccessorEntry(a.IdentityUserId, a.AccessTypeId)));

        var canEdit = AppointmentAccessRules.CanEdit(
            callerUserId: CurrentUser.Id,
            callerIsInternalUser: isInternal,
            appointmentCreatorId: appointment.CreatorId,
            accessorEntries: entries);

        if (!canEdit)
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.ChangeRequestEditAccessRequired)
                .WithData("appointmentId", appointmentId);
        }
    }
}
