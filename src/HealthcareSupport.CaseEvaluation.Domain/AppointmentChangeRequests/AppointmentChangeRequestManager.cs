using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.DoctorAvailabilities;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using HealthcareSupport.CaseEvaluation.SystemParameters;
using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.EventBus.Local;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

/// <summary>
/// Domain service for the cancel / reschedule lifecycle. Phase 15
/// (2026-05-04) ships <see cref="SubmitCancellationAsync"/>; Phase 16
/// will add <c>SubmitRescheduleAsync</c>; Phase 17 (Session B) adds
/// the supervisor-side <c>ApproveCancellationAsync</c>,
/// <c>RejectCancellationAsync</c>, <c>ApproveRescheduleAsync</c>,
/// <c>RejectRescheduleAsync</c>.
/// </summary>
public class AppointmentChangeRequestManager : DomainService
{
    private readonly IAppointmentChangeRequestRepository _repository;
    // Phase 15 / 16 (2026-05-04) -- additional collaborators wired in
    // for the SubmitCancellationAsync + SubmitRescheduleAsync flows.
    // The thinner ctor stays for any existing consumer that only calls
    // GetAsync.
    private readonly IAppointmentRepository? _appointmentRepository;
    private readonly IRepository<DoctorAvailability, Guid>? _doctorAvailabilityRepository;
    private readonly ISystemParameterRepository? _systemParameterRepository;
    private readonly ILocalEventBus? _localEventBus;
    // Phase 16 (2026-05-04) -- transition the parent appointment via
    // the existing state machine. Optional (only the reschedule path
    // touches it) but resolves cleanly via DI when the full ctor is used.
    private readonly AppointmentManager? _appointmentManager;

    public AppointmentChangeRequestManager(IAppointmentChangeRequestRepository repository)
    {
        _repository = repository;
    }

    public AppointmentChangeRequestManager(
        IAppointmentChangeRequestRepository repository,
        IAppointmentRepository appointmentRepository,
        IRepository<DoctorAvailability, Guid> doctorAvailabilityRepository,
        ISystemParameterRepository systemParameterRepository,
        ILocalEventBus localEventBus,
        AppointmentManager appointmentManager)
        : this(repository)
    {
        _appointmentRepository = appointmentRepository;
        _doctorAvailabilityRepository = doctorAvailabilityRepository;
        _systemParameterRepository = systemParameterRepository;
        _localEventBus = localEventBus;
        _appointmentManager = appointmentManager;
    }

    /// <summary>
    /// Loads a change request by id. Throws if not found.
    /// </summary>
    public virtual Task<AppointmentChangeRequest> GetAsync(Guid id) => _repository.GetAsync(id);

    /// <summary>
    /// Phase 15 (2026-05-04) -- OLD-parity cancellation submit. Loads
    /// the source appointment and the slot it sits on, validates the
    /// status (must be Approved) + the cancel-time window, and inserts
    /// a new <see cref="AppointmentChangeRequest"/> with
    /// <see cref="ChangeRequestType.Cancel"/>. Per OLD parity the
    /// parent appointment STAYS at <see cref="AppointmentStatusType.Approved"/>
    /// while the change request is Pending; the supervisor's approve
    /// flow (Phase 17) writes the terminal CancelledNoBill /
    /// CancelledLate status onto the parent.
    ///
    /// Cancellation reason is required (validated by the entity's
    /// constructor's <c>Check.NotNullOrWhiteSpace</c>).
    /// </summary>
    /// <param name="appointmentId">The appointment to cancel.</param>
    /// <param name="cancellationReason">Verbatim reason supplied by the user.</param>
    /// <param name="acting">
    /// Identity of the caller -- threaded through to the ETO so the
    /// notification handler can address the requester correctly.
    /// </param>
    /// <returns>The persisted change-request row.</returns>
    public virtual async Task<AppointmentChangeRequest> SubmitCancellationAsync(
        Guid appointmentId,
        string cancellationReason,
        Guid? actingUserId)
    {
        if (_appointmentRepository == null
            || _doctorAvailabilityRepository == null
            || _systemParameterRepository == null
            || _localEventBus == null)
        {
            throw new InvalidOperationException(
                "AppointmentChangeRequestManager.SubmitCancellationAsync requires the full DI ctor; resolve via the container or pass the additional collaborators.");
        }

        Check.NotDefaultOrNull<Guid>(appointmentId, nameof(appointmentId));
        Check.NotNullOrWhiteSpace(cancellationReason, nameof(cancellationReason));

        var appointment = await _appointmentRepository.FindAsync(appointmentId);
        if (appointment == null)
        {
            throw new EntityNotFoundException(typeof(Appointment), appointmentId);
        }

        if (!CancellationRequestValidators.CanRequestCancellation(appointment.AppointmentStatus))
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.ChangeRequestAppointmentNotApproved)
                .WithData("appointmentId", appointmentId)
                .WithData("status", appointment.AppointmentStatus);
        }

        // Cancel-time gate -- per OLD AppointmentChangeRequestDomain.cs:83-90
        // reads SystemParameter.AppointmentCancelTime per tenant and
        // rejects if the slot date is closer than that threshold.
        var slot = await _doctorAvailabilityRepository.FindAsync(appointment.DoctorAvailabilityId);
        if (slot == null)
        {
            throw new EntityNotFoundException(typeof(DoctorAvailability), appointment.DoctorAvailabilityId);
        }

        var systemParameter = await _systemParameterRepository.GetCurrentTenantAsync();
        if (systemParameter == null)
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.SystemParameterNotSeeded);
        }
        var cancelTimeDays = systemParameter.AppointmentCancelTime;
        if (CancellationRequestValidators.IsWithinNoCancelWindow(slot.AvailableDate, DateTime.Today, cancelTimeDays))
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.ChangeRequestCancelTimeWindow)
                .WithData("cancelTimeDays", cancelTimeDays)
                .WithData("slotDate", slot.AvailableDate);
        }

        var changeRequest = new AppointmentChangeRequest(
            id: GuidGenerator.Create(),
            tenantId: appointment.TenantId,
            appointmentId: appointmentId,
            changeRequestType: ChangeRequestType.Cancel,
            cancellationReason: cancellationReason,
            reScheduleReason: null,
            newDoctorAvailabilityId: null,
            isBeyondLimit: false);

        await _repository.InsertAsync(changeRequest);

        await _localEventBus.PublishAsync(new AppointmentChangeRequestSubmittedEto
        {
            AppointmentId = appointmentId,
            ChangeRequestId = changeRequest.Id,
            TenantId = appointment.TenantId,
            ChangeRequestType = ChangeRequestType.Cancel,
            SubmittedByUserId = actingUserId ?? Guid.Empty,
            OccurredAt = DateTime.UtcNow,
        });

        return changeRequest;
    }

    /// <summary>
    /// Phase 16 (2026-05-04) -- OLD-parity reschedule submit. Loads the
    /// source appointment and the user-picked new slot, validates
    /// status (Approved) + slot availability, inserts a Pending
    /// <see cref="AppointmentChangeRequest"/> with
    /// <see cref="ChangeRequestType.Reschedule"/>, transitions the
    /// new slot Available -> Reserved (interim hold pending supervisor
    /// approval), and transitions the parent appointment Approved ->
    /// RescheduleRequested via the state machine. Mirrors OLD
    /// <c>AppointmentChangeRequestDomain.cs:197-223</c>.
    ///
    /// Lead-time + per-AppointmentType max-time gates run UPSTREAM of
    /// this method via the Application-layer
    /// <c>BookingPolicyValidator</c> -- same gates as the booking
    /// flow per OLD parity.
    /// </summary>
    /// <param name="appointmentId">Source appointment to reschedule.</param>
    /// <param name="newDoctorAvailabilityId">User-picked new slot.</param>
    /// <param name="reScheduleReason">Verbatim reason supplied by the user.</param>
    /// <param name="isBeyondLimit">
    /// Admin override flag. External-user submits always pass false;
    /// the field is preserved on the entity so a future admin-side
    /// path can set it.
    /// </param>
    /// <param name="actingUserId">Caller, threaded through to the ETO.</param>
    public virtual async Task<AppointmentChangeRequest> SubmitRescheduleAsync(
        Guid appointmentId,
        Guid newDoctorAvailabilityId,
        string reScheduleReason,
        bool isBeyondLimit,
        Guid? actingUserId)
    {
        if (_appointmentRepository == null
            || _doctorAvailabilityRepository == null
            || _localEventBus == null
            || _appointmentManager == null)
        {
            throw new InvalidOperationException(
                "AppointmentChangeRequestManager.SubmitRescheduleAsync requires the full DI ctor; resolve via the container or pass the additional collaborators.");
        }

        Check.NotDefaultOrNull<Guid>(appointmentId, nameof(appointmentId));
        if (newDoctorAvailabilityId == Guid.Empty)
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.ChangeRequestNewSlotRequired);
        }
        if (string.IsNullOrWhiteSpace(reScheduleReason))
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.ChangeRequestRescheduleReasonRequired);
        }

        var appointment = await _appointmentRepository.FindAsync(appointmentId);
        if (appointment == null)
        {
            throw new EntityNotFoundException(typeof(Appointments.Appointment), appointmentId);
        }

        if (!RescheduleRequestValidators.CanRequestReschedule(appointment.AppointmentStatus))
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.ChangeRequestAppointmentNotApproved)
                .WithData("appointmentId", appointmentId)
                .WithData("status", appointment.AppointmentStatus);
        }

        var newSlot = await _doctorAvailabilityRepository.FindAsync(newDoctorAvailabilityId);
        if (newSlot == null)
        {
            throw new EntityNotFoundException(typeof(DoctorAvailability), newDoctorAvailabilityId);
        }

        if (!RescheduleRequestValidators.IsSlotAvailable(newSlot.BookingStatusId))
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.ChangeRequestNewSlotNotAvailable)
                .WithData("newSlotId", newDoctorAvailabilityId)
                .WithData("currentStatus", newSlot.BookingStatusId);
        }

        var changeRequest = new AppointmentChangeRequest(
            id: GuidGenerator.Create(),
            tenantId: appointment.TenantId,
            appointmentId: appointmentId,
            changeRequestType: ChangeRequestType.Reschedule,
            cancellationReason: null,
            reScheduleReason: reScheduleReason,
            newDoctorAvailabilityId: newDoctorAvailabilityId,
            isBeyondLimit: isBeyondLimit);

        await _repository.InsertAsync(changeRequest);

        // Transition the NEW slot Available -> Reserved. The OLD slot
        // (appointment.DoctorAvailabilityId) stays Booked while the
        // change request is Pending -- the supervisor's approve flow
        // (Phase 17) releases it.
        newSlot.BookingStatusId = HealthcareSupport.CaseEvaluation.Enums.BookingStatus.Reserved;
        await _doctorAvailabilityRepository.UpdateAsync(newSlot);

        // Transition the parent appointment Approved -> RescheduleRequested
        // via the state machine. Publishes its own AppointmentStatusChangedEto
        // for any downstream subscribers; we additionally publish the
        // change-request-submitted event below for the per-event email
        // template fan-out.
        await _appointmentManager.RequestRescheduleAsync(appointmentId, reScheduleReason, actingUserId);

        await _localEventBus.PublishAsync(new AppointmentChangeRequestSubmittedEto
        {
            AppointmentId = appointmentId,
            ChangeRequestId = changeRequest.Id,
            TenantId = appointment.TenantId,
            ChangeRequestType = ChangeRequestType.Reschedule,
            SubmittedByUserId = actingUserId ?? Guid.Empty,
            OccurredAt = DateTime.UtcNow,
        });

        return changeRequest;
    }
}
