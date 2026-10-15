using System;
using HealthcareSupport.CaseEvaluation.Enums;
using Volo.Abp;

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
    /// reschedule request is allowed. External users stay OLD-parity:
    /// only <see cref="AppointmentStatusType.Approved"/> (else
    /// "NoChangeAllowedinAppointment"). B1 (2026-07-01): internal staff
    /// may also reschedule a not-yet-approved appointment, so
    /// <paramref name="allowPendingSource"/> additionally admits
    /// <see cref="AppointmentStatusType.Pending"/>. A Pending-source
    /// reschedule leaves the parent Pending (no
    /// Pending -&gt; RescheduleRequested transition exists); the
    /// orchestrator skips the state-machine step accordingly.
    /// </summary>
    public static bool CanRequestReschedule(AppointmentStatusType appointmentStatus, bool allowPendingSource)
    {
        if (appointmentStatus == AppointmentStatusType.Approved)
        {
            return true;
        }

        return allowPendingSource && appointmentStatus == AppointmentStatusType.Pending;
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

    /// <summary>
    /// 2026-10-15 -- throws when a reschedule proposes the slot the appointment already holds.
    /// Moving an appointment to where it already is asks both parties to consent to nothing, and
    /// completing it closes the original as Rescheduled and books a copy onto the same slot. The
    /// slot-status check above does not catch it: under the capacity model a booked slot can
    /// still read Available. Shared by the staff-filed submit and the staff "Set date" step.
    /// </summary>
    public static void EnsureNotCurrentSlot(Guid currentSlotId, Guid proposedSlotId)
    {
        if (proposedSlotId == currentSlotId)
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.ChangeRequestNewSlotIsCurrentSlot)
                .WithData("slotId", proposedSlotId);
        }
    }
}
