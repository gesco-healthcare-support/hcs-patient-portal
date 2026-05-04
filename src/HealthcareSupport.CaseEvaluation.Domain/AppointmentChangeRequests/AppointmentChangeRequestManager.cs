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
    // Phase 15 (2026-05-04) -- additional collaborators wired in for
    // the SubmitCancellationAsync flow. The thinner ctor stays for any
    // existing consumer that only calls GetAsync.
    private readonly IAppointmentRepository? _appointmentRepository;
    private readonly IRepository<DoctorAvailability, Guid>? _doctorAvailabilityRepository;
    private readonly ISystemParameterRepository? _systemParameterRepository;
    private readonly ILocalEventBus? _localEventBus;

    public AppointmentChangeRequestManager(IAppointmentChangeRequestRepository repository)
    {
        _repository = repository;
    }

    public AppointmentChangeRequestManager(
        IAppointmentChangeRequestRepository repository,
        IAppointmentRepository appointmentRepository,
        IRepository<DoctorAvailability, Guid> doctorAvailabilityRepository,
        ISystemParameterRepository systemParameterRepository,
        ILocalEventBus localEventBus)
        : this(repository)
    {
        _appointmentRepository = appointmentRepository;
        _doctorAvailabilityRepository = doctorAvailabilityRepository;
        _systemParameterRepository = systemParameterRepository;
        _localEventBus = localEventBus;
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
}
