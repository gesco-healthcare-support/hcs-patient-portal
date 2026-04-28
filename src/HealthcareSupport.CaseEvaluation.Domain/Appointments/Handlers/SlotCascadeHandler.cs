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
/// Slot transitions wired here (W1-1 subset):
///   Pending -> Approved              : Reserved -> Booked
///   Pending -> Rejected              : Reserved -> Available
///   Pending -> AwaitingMoreInfo      : (no change; slot stays Reserved)
///   AwaitingMoreInfo -> Pending      : (no change; slot stays Reserved)
///   AwaitingMoreInfo -> Approved     : Reserved -> Booked
///   AwaitingMoreInfo -> Rejected     : Reserved -> Available
///
/// Cancel / reschedule slot cascades land in Wave 3 once those endpoints
/// are exposed. Slot transitions for those states are NOT yet wired here;
/// when Wave 3 lands the matching endpoints, extend the switch below.
///
/// Runs inside the same UoW as the manager-side transition (ABP's
/// <see cref="ILocalEventBus"/> publishes synchronously; subscribers complete
/// before the UoW commits, so a slot-flip failure rolls back the appointment
/// transition too).
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
        var targetSlotStatus = MapToSlotStatus(eventData.FromStatus, eventData.ToStatus);
        if (targetSlotStatus == null)
        {
            return;
        }

        var appointment = await _appointmentRepository.FindAsync(eventData.AppointmentId);
        if (appointment == null)
        {
            _logger.LogWarning(
                "SlotCascadeHandler: appointment {AppointmentId} not found; skipping slot flip.",
                eventData.AppointmentId);
            return;
        }

        var slot = await _availabilityRepository.FindAsync(appointment.DoctorAvailabilityId);
        if (slot == null)
        {
            _logger.LogWarning(
                "SlotCascadeHandler: slot {DoctorAvailabilityId} for appointment {AppointmentId} not found; skipping slot flip.",
                appointment.DoctorAvailabilityId,
                eventData.AppointmentId);
            return;
        }

        if (slot.BookingStatusId == targetSlotStatus.Value)
        {
            return;
        }

        slot.BookingStatusId = targetSlotStatus.Value;
        await _availabilityRepository.UpdateAsync(slot, autoSave: true);
    }

    private static BookingStatus? MapToSlotStatus(AppointmentStatusType from, AppointmentStatusType to)
    {
        return to switch
        {
            AppointmentStatusType.Approved => BookingStatus.Booked,
            AppointmentStatusType.Rejected => BookingStatus.Available,
            AppointmentStatusType.AwaitingMoreInfo => null,
            AppointmentStatusType.Pending when from == AppointmentStatusType.AwaitingMoreInfo => null,
            _ => null,
        };
    }
}
