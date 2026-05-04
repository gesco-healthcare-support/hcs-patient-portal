using HealthcareSupport.CaseEvaluation.Enums;
using System;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

/// <summary>
/// Phase 15 (2026-05-04) -- pure validators backing
/// <see cref="AppointmentChangeRequestManager.SubmitCancellationAsync"/>.
/// Mirrors OLD
/// <c>P:\PatientPortalOld\PatientAppointment.Domain\AppointmentRequestModule\AppointmentChangeRequestDomain.cs</c>
/// lines 73-95 (AddValidation cancel branch):
///   - Appointment must be in status <c>Approved</c>
///     (else "NoChangeAllowedinAppointment").
///   - Cancel-time gate: <c>(slot.AvailableDate - DateTime.Today).TotalDays
///     &lt; SystemParameters.AppointmentCancelTime</c> rejects the request
///     (else "CannotCancelOrRescheduleAppointment").
///   - <c>CancellationReason</c> must be present (handled by the entity
///     constructor's <c>Check.NotNullOrWhiteSpace</c>; surfaced as
///     "ProvideCancelReason" by callers if they want to short-circuit
///     before the entity is built).
///
/// Pure: takes only the values needed and returns the decision so the
/// orchestrator can assemble the right error code without re-reading
/// state.
/// </summary>
public static class CancellationRequestValidators
{
    /// <summary>
    /// Returns true when the appointment is in a state where a cancel
    /// request is allowed. OLD only allows the cancel flow on
    /// <see cref="AppointmentStatusType.Approved"/>; everything else
    /// rejects with "NoChangeAllowedinAppointment".
    /// </summary>
    public static bool CanRequestCancellation(AppointmentStatusType appointmentStatus)
    {
        return appointmentStatus == AppointmentStatusType.Approved;
    }

    /// <summary>
    /// Returns true when the cancel attempt is INSIDE the no-cancel
    /// window -- i.e. the appointment date is closer than the
    /// per-tenant <paramref name="cancelTimeDays"/> threshold. The
    /// orchestrator throws when this returns true.
    ///
    /// Mirrors OLD line 87:
    ///   <c>(appointmentDateTime - DateTime.Today).TotalDays &lt;
    ///   systemAppointmentCancelTime</c>
    ///
    /// Note OLD uses strict less-than (a slot exactly
    /// <c>cancelTimeDays</c> out is still cancellable). Strict parity
    /// preserves that boundary.
    /// </summary>
    public static bool IsWithinNoCancelWindow(DateTime slotDate, DateTime today, int cancelTimeDays)
    {
        var daysUntilSlot = (slotDate.Date - today.Date).TotalDays;
        return daysUntilSlot < cancelTimeDays;
    }
}
