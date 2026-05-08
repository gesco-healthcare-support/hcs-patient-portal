using HealthcareSupport.CaseEvaluation.Enums;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

/// <summary>
/// Phase 16 (2026-05-04) -- pure validators backing
/// <see cref="AppointmentChangeRequestManager.SubmitRescheduleAsync"/>.
/// Mirrors OLD
/// <c>P:\PatientPortalOld\PatientAppointment.Domain\AppointmentRequestModule\AppointmentChangeRequestDomain.cs</c>
/// lines 96-122 (AddValidation reschedule branch):
///   - Appointment must be in status <c>Approved</c>
///     (else "NoChangeAllowedinAppointment" -- shared with cancel
///     branch via Phase 15's <see cref="CancellationRequestValidators"/>).
///   - <c>ReScheduleReason</c> must be present (else "ProvideRescheduleReason").
///   - <c>NewDoctorAvailabilityId</c> must be supplied (else
///     "ProvideNewAppointmentDateTime").
///   - The new slot's <c>BookingStatusId</c> must be
///     <see cref="BookingStatus.Available"/> (else
///     "AppointmentBookingDateNotAvailable").
///
/// Lead-time + per-AppointmentType max-time gates run in the
/// Application layer via the existing
/// <c>BookingPolicyValidator</c> (Phase 11b) -- same gates apply to
/// the booking flow and the reschedule flow per OLD parity.
/// </summary>
public static class RescheduleRequestValidators
{
    /// <summary>
    /// Returns true when the appointment is in a state where a
    /// reschedule request is allowed. OLD only allows the reschedule
    /// flow on <see cref="AppointmentStatusType.Approved"/>; everything
    /// else rejects with "NoChangeAllowedinAppointment".
    /// </summary>
    public static bool CanRequestReschedule(AppointmentStatusType appointmentStatus)
    {
        return appointmentStatus == AppointmentStatusType.Approved;
    }

    /// <summary>
    /// Returns true when the new slot is currently
    /// <see cref="BookingStatus.Available"/>. Slots already in
    /// <see cref="BookingStatus.Reserved"/> or
    /// <see cref="BookingStatus.Booked"/> cannot host a new
    /// reschedule request.
    /// </summary>
    public static bool IsSlotAvailable(BookingStatus slotStatus)
    {
        return slotStatus == BookingStatus.Available;
    }
}
