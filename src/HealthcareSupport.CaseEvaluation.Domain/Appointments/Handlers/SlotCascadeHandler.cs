using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.DoctorAvailabilities;
using HealthcareSupport.CaseEvaluation.Enums;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus;
using Volo.Abp.EventBus.Local;
using Volo.Abp.Uow;

namespace HealthcareSupport.CaseEvaluation.Appointments.Handlers;

/// <summary>
/// Subscribes to <see cref="AppointmentStatusChangedEto"/> and flips the
/// appointment's <see cref="DoctorAvailability"/> slot status per the
/// T11 (`docs/product/cross-cutting/appointment-lifecycle.md`) sync table.
///
/// W2-3 expanded the cap from W1-1's 4-state subset to the full 14-state
/// status -&gt; slot mapping (canonical OLD-side mapping per the Wave 2 plan
/// deep-dive). Initial-create (FromStatus == null) and hard-delete
/// (ToStatus == null) paths fire through here too, so AppointmentsAppService
/// no longer mutates slots inline.
///
/// Status -&gt; target slot:
///   Pending                 : Reserved   (booker submitted; held)
///   Approved                : Booked     (confirmed)
///   Rejected                : Available  (freed)
///   AwaitingMoreInfo        : Reserved   (held during send-back)
///   NoShow                  : Booked     (terminal; keep booked for billing)
///   CheckedIn / CheckedOut  : Booked     (in-flight)
///   Billed                  : Booked     (terminal)
///   RescheduleRequested     : Booked     (still booked until confirmed)
///   CancellationRequested   : Booked     (still booked until confirmed)
///   CancelledNoBill / CancelledLate         : Available  (freed)
///   RescheduledNoBill / RescheduledLate     : Available  (original slot freed)
///   (delete; ToStatus == null)              : Available  (freed)
///
/// Reschedule swap: when <see cref="AppointmentStatusChangedEto.OldDoctorAvailabilityId"/>
/// is set, the OLD slot is forced to Available and the NEW slot is forced to
/// the target status (typically Reserved or Booked). The simple non-swap
/// path just flips the current slot.
///
/// Runs inside the same UoW as the publisher (ABP's <see cref="ILocalEventBus"/>
/// publishes synchronously; subscribers complete before the UoW commits, so
/// a slot-flip failure rolls back the appointment change too).
///
/// Distributed-lock wrap: NOT applied at MVP. Single-node demo doesn't race;
/// multi-node prod hardening is post-MVP per the W2-3 deep-dive.
/// </summary>
public class SlotCascadeHandler :
    ILocalEventHandler<AppointmentStatusChangedEto>,
    ITransientDependency
{
    private readonly IRepository<DoctorAvailability, Guid> _availabilityRepository;
    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly ILogger<SlotCascadeHandler> _logger;

    public SlotCascadeHandler(
        IRepository<DoctorAvailability, Guid> availabilityRepository,
        IRepository<Appointment, Guid> appointmentRepository,
        ILogger<SlotCascadeHandler> logger)
    {
        _availabilityRepository = availabilityRepository;
        _appointmentRepository = appointmentRepository;
        _logger = logger;
    }

    public virtual async Task HandleEventAsync(AppointmentStatusChangedEto eventData)
    {
        // 1. Reschedule swap: old slot -> Available, new slot -> target.
        if (eventData.OldDoctorAvailabilityId.HasValue
            && eventData.DoctorAvailabilityId.HasValue
            && eventData.OldDoctorAvailabilityId.Value != eventData.DoctorAvailabilityId.Value)
        {
            await ApplySlotStatusAsync(eventData.OldDoctorAvailabilityId.Value, BookingStatus.Available, eventData.AppointmentId);
            var newTarget = MapToSlotStatus(eventData.FromStatus, eventData.ToStatus);
            if (newTarget.HasValue)
            {
                await ApplySlotStatusAsync(eventData.DoctorAvailabilityId.Value, newTarget.Value, eventData.AppointmentId);
            }
            return;
        }

        // 2. Single-slot path: derive target slot status from the transition.
        var targetSlotStatus = MapToSlotStatus(eventData.FromStatus, eventData.ToStatus);
        if (targetSlotStatus == null)
        {
            return;
        }

        // 3. Resolve the slot ID. Prefer the snapshotted ETO field (works when
        // the appointment is already deleted); fall back to re-fetching the
        // appointment for back-compat with publishers that don't set the
        // snapshot.
        var slotId = eventData.DoctorAvailabilityId;
        if (!slotId.HasValue)
        {
            var appointment = await _appointmentRepository.FindAsync(eventData.AppointmentId);
            if (appointment == null)
            {
                _logger.LogWarning(
                    "SlotCascadeHandler: appointment {AppointmentId} not found and ETO carried no DoctorAvailabilityId; skipping slot flip.",
                    eventData.AppointmentId);
                return;
            }
            slotId = appointment.DoctorAvailabilityId;
        }

        await ApplySlotStatusAsync(slotId.Value, targetSlotStatus.Value, eventData.AppointmentId);
    }

    private async Task ApplySlotStatusAsync(Guid slotId, BookingStatus targetStatus, Guid appointmentId)
    {
        var slot = await _availabilityRepository.FindAsync(slotId);
        if (slot == null)
        {
            _logger.LogWarning(
                "SlotCascadeHandler: slot {DoctorAvailabilityId} for appointment {AppointmentId} not found; skipping slot flip.",
                slotId,
                appointmentId);
            return;
        }

        if (slot.BookingStatusId == targetStatus)
        {
            return;
        }

        slot.BookingStatusId = targetStatus;
        await _availabilityRepository.UpdateAsync(slot, autoSave: true);
    }

    private static BookingStatus? MapToSlotStatus(AppointmentStatusType? from, AppointmentStatusType? to)
    {
        // Hard delete -- free the slot regardless of prior status.
        if (!to.HasValue)
        {
            return BookingStatus.Available;
        }

        return to.Value switch
        {
            AppointmentStatusType.Pending => BookingStatus.Reserved,
            AppointmentStatusType.Approved => BookingStatus.Booked,
            AppointmentStatusType.Rejected => BookingStatus.Available,
            AppointmentStatusType.AwaitingMoreInfo => BookingStatus.Reserved,
            AppointmentStatusType.NoShow => BookingStatus.Booked,
            AppointmentStatusType.CheckedIn => BookingStatus.Booked,
            AppointmentStatusType.CheckedOut => BookingStatus.Booked,
            AppointmentStatusType.Billed => BookingStatus.Booked,
            AppointmentStatusType.RescheduleRequested => BookingStatus.Booked,
            AppointmentStatusType.CancellationRequested => BookingStatus.Booked,
            AppointmentStatusType.CancelledNoBill => BookingStatus.Available,
            AppointmentStatusType.CancelledLate => BookingStatus.Available,
            AppointmentStatusType.RescheduledNoBill => BookingStatus.Available,
            AppointmentStatusType.RescheduledLate => BookingStatus.Available,
            _ => null,
        };
    }
}
