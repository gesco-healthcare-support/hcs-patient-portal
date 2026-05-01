using HealthcareSupport.CaseEvaluation.Enums;
using Stateless;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.EventBus.Local;
using Volo.Abp.Data;

namespace HealthcareSupport.CaseEvaluation.Appointments;

public class AppointmentManager : DomainService
{
    protected IAppointmentRepository _appointmentRepository;
    protected IRepository<AppointmentSendBackInfo, Guid> _sendBackInfoRepository;
    protected ILocalEventBus _localEventBus;

    public AppointmentManager(
        IAppointmentRepository appointmentRepository,
        IRepository<AppointmentSendBackInfo, Guid> sendBackInfoRepository,
        ILocalEventBus localEventBus)
    {
        _appointmentRepository = appointmentRepository;
        _sendBackInfoRepository = sendBackInfoRepository;
        _localEventBus = localEventBus;
    }

    public virtual async Task<Appointment> CreateAsync(Guid patientId, Guid identityUserId, Guid appointmentTypeId, Guid locationId, Guid doctorAvailabilityId, DateTime appointmentDate, string requestConfirmationNumber, AppointmentStatusType appointmentStatus, string? panelNumber = null, DateTime? dueDate = null)
    {
        Check.NotNull(patientId, nameof(patientId));
        Check.NotNull(identityUserId, nameof(identityUserId));
        Check.NotNull(appointmentTypeId, nameof(appointmentTypeId));
        Check.NotNull(locationId, nameof(locationId));
        Check.NotNull(doctorAvailabilityId, nameof(doctorAvailabilityId));
        Check.NotNull(appointmentDate, nameof(appointmentDate));
        Check.NotNullOrWhiteSpace(requestConfirmationNumber, nameof(requestConfirmationNumber));
        Check.Length(requestConfirmationNumber, nameof(requestConfirmationNumber), AppointmentConsts.RequestConfirmationNumberMaxLength);
        Check.NotNull(appointmentStatus, nameof(appointmentStatus));
        Check.Length(panelNumber, nameof(panelNumber), AppointmentConsts.PanelNumberMaxLength);
        var appointment = new Appointment(GuidGenerator.Create(), patientId, identityUserId, appointmentTypeId, locationId, doctorAvailabilityId, appointmentDate, requestConfirmationNumber, appointmentStatus, panelNumber, dueDate);
        return await _appointmentRepository.InsertAsync(appointment);
    }

    public virtual async Task<Appointment> UpdateAsync(Guid id, Guid patientId, Guid identityUserId, Guid appointmentTypeId, Guid locationId, Guid doctorAvailabilityId, DateTime appointmentDate, string? panelNumber = null, DateTime? dueDate = null, [CanBeNull] string? concurrencyStamp = null)
    {
        Check.NotNull(patientId, nameof(patientId));
        Check.NotNull(identityUserId, nameof(identityUserId));
        Check.NotNull(appointmentTypeId, nameof(appointmentTypeId));
        Check.NotNull(locationId, nameof(locationId));
        Check.NotNull(doctorAvailabilityId, nameof(doctorAvailabilityId));
        Check.NotNull(appointmentDate, nameof(appointmentDate));
        Check.Length(panelNumber, nameof(panelNumber), AppointmentConsts.PanelNumberMaxLength);
        var appointment = await _appointmentRepository.GetAsync(id);
        appointment.PatientId = patientId;
        appointment.IdentityUserId = identityUserId;
        appointment.AppointmentTypeId = appointmentTypeId;
        appointment.LocationId = locationId;
        appointment.DoctorAvailabilityId = doctorAvailabilityId;
        appointment.AppointmentDate = appointmentDate;
        appointment.PanelNumber = panelNumber;
        appointment.DueDate = dueDate;
        appointment.SetConcurrencyStampIfNotNull(concurrencyStamp);
        return await _appointmentRepository.UpdateAsync(appointment);
    }

    /// <summary>
    /// W1-1 transition: Pending|AwaitingMoreInfo -> Approved.
    /// Stamps <c>AppointmentApproveDate</c> on entry; publishes
    /// <see cref="AppointmentStatusChangedEto"/> for slot-cascade + email subscribers.
    /// </summary>
    public virtual Task<Appointment> ApproveAsync(Guid id, Guid? actingUserId)
        => TransitionAsync(id, AppointmentTransitionTrigger.Approve, reason: null, actingUserId);

    /// <summary>W1-1 transition: Pending|AwaitingMoreInfo -> Rejected.</summary>
    public virtual Task<Appointment> RejectAsync(Guid id, string? reason, Guid? actingUserId)
        => TransitionAsync(id, AppointmentTransitionTrigger.Reject, reason, actingUserId);

    /// <summary>
    /// W1-1 transition: Pending -> AwaitingMoreInfo. Persists an
    /// <see cref="AppointmentSendBackInfo"/> row capturing the office's flagged
    /// fields + freeform note before firing the trigger.
    /// </summary>
    public virtual async Task<Appointment> SendBackAsync(Guid id, IEnumerable<string> flaggedFields, string? note, Guid? actingUserId)
    {
        var appointment = await _appointmentRepository.GetAsync(id);

        var sendBackRow = new AppointmentSendBackInfo(
            GuidGenerator.Create(),
            appointment.TenantId,
            appointment.Id,
            flaggedFields ?? Enumerable.Empty<string>(),
            note,
            actingUserId);
        await _sendBackInfoRepository.InsertAsync(sendBackRow, autoSave: true);

        return await ApplyTransitionAsync(appointment, AppointmentTransitionTrigger.SendBack, note, actingUserId);
    }

    /// <summary>
    /// W1-1 auto-transition: AwaitingMoreInfo -> Pending. Fires when the booker
    /// re-submits the booking form. Marks the latest unresolved
    /// <see cref="AppointmentSendBackInfo"/> row as resolved.
    /// </summary>
    public virtual async Task<Appointment> SaveAndResubmitAsync(Guid id, Guid? actingUserId)
    {
        var appointment = await _appointmentRepository.GetAsync(id);

        var sendBackQueryable = await _sendBackInfoRepository.GetQueryableAsync();
        var latestUnresolved = sendBackQueryable
            .Where(x => x.AppointmentId == id && !x.IsResolved)
            .OrderByDescending(x => x.SentBackAt)
            .FirstOrDefault();
        if (latestUnresolved != null)
        {
            latestUnresolved.MarkResolved();
            await _sendBackInfoRepository.UpdateAsync(latestUnresolved, autoSave: true);
        }

        return await ApplyTransitionAsync(appointment, AppointmentTransitionTrigger.SaveAndResubmit, reason: null, actingUserId);
    }

    private async Task<Appointment> TransitionAsync(Guid id, AppointmentTransitionTrigger trigger, string? reason, Guid? actingUserId)
    {
        var appointment = await _appointmentRepository.GetAsync(id);
        return await ApplyTransitionAsync(appointment, trigger, reason, actingUserId);
    }

    private async Task<Appointment> ApplyTransitionAsync(Appointment appointment, AppointmentTransitionTrigger trigger, string? reason, Guid? actingUserId)
    {
        var fromStatus = appointment.AppointmentStatus;
        var machine = BuildMachine(appointment);

        if (!machine.CanFire(trigger))
        {
            throw new BusinessException("CaseEvaluation:AppointmentInvalidTransition")
                .WithData("from", fromStatus)
                .WithData("trigger", trigger);
        }

        machine.Fire(trigger);

        if (trigger == AppointmentTransitionTrigger.Approve)
        {
            appointment.AppointmentApproveDate = DateTime.UtcNow;
        }

        await _appointmentRepository.UpdateAsync(appointment, autoSave: true);

        // W2-3: snapshot the slot ID on the ETO so SlotCascadeHandler doesn't
        // re-fetch the appointment for the common single-slot transition path.
        await _localEventBus.PublishAsync(new AppointmentStatusChangedEto(
            appointment.Id,
            appointment.TenantId,
            fromStatus,
            appointment.AppointmentStatus,
            actingUserId,
            reason,
            DateTime.UtcNow,
            doctorAvailabilityId: appointment.DoctorAvailabilityId));

        return appointment;
    }

    /// <summary>
    /// Builds the appointment status state machine. All 14 transitions are
    /// configured declaratively; W1-1 only exposes endpoints for Approve /
    /// Reject / SendBack / SaveAndResubmit. Cancel / Reschedule / day-of-exam triggers
    /// are reachable in the graph but unreachable through the API surface
    /// until Wave 3 (appointment-change-requests).
    /// </summary>
    private static StateMachine<AppointmentStatusType, AppointmentTransitionTrigger> BuildMachine(Appointment appointment)
    {
        var machine = new StateMachine<AppointmentStatusType, AppointmentTransitionTrigger>(
            () => appointment.AppointmentStatus,
            s => appointment.AppointmentStatus = s);

        machine.Configure(AppointmentStatusType.Pending)
            .Permit(AppointmentTransitionTrigger.Approve, AppointmentStatusType.Approved)
            .Permit(AppointmentTransitionTrigger.Reject, AppointmentStatusType.Rejected)
            .Permit(AppointmentTransitionTrigger.SendBack, AppointmentStatusType.AwaitingMoreInfo);

        machine.Configure(AppointmentStatusType.AwaitingMoreInfo)
            .Permit(AppointmentTransitionTrigger.SaveAndResubmit, AppointmentStatusType.Pending)
            .Permit(AppointmentTransitionTrigger.Approve, AppointmentStatusType.Approved)
            .Permit(AppointmentTransitionTrigger.Reject, AppointmentStatusType.Rejected);

        machine.Configure(AppointmentStatusType.Approved)
            .Permit(AppointmentTransitionTrigger.RequestCancellation, AppointmentStatusType.CancellationRequested)
            .Permit(AppointmentTransitionTrigger.RequestReschedule, AppointmentStatusType.RescheduleRequested)
            .Permit(AppointmentTransitionTrigger.MarkNoShow, AppointmentStatusType.NoShow)
            .Permit(AppointmentTransitionTrigger.CheckIn, AppointmentStatusType.CheckedIn);

        machine.Configure(AppointmentStatusType.CancellationRequested)
            .Permit(AppointmentTransitionTrigger.ConfirmCancellation, AppointmentStatusType.CancelledNoBill)
            .Permit(AppointmentTransitionTrigger.ConfirmCancellationLate, AppointmentStatusType.CancelledLate);

        machine.Configure(AppointmentStatusType.RescheduleRequested)
            .Permit(AppointmentTransitionTrigger.ConfirmReschedule, AppointmentStatusType.RescheduledNoBill)
            .Permit(AppointmentTransitionTrigger.ConfirmRescheduleLate, AppointmentStatusType.RescheduledLate);

        machine.Configure(AppointmentStatusType.CheckedIn)
            .Permit(AppointmentTransitionTrigger.CheckOut, AppointmentStatusType.CheckedOut);

        machine.Configure(AppointmentStatusType.CheckedOut)
            .Permit(AppointmentTransitionTrigger.Bill, AppointmentStatusType.Billed);

        return machine;
    }
}
