using HealthcareSupport.CaseEvaluation.Enums;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

/// <summary>
/// B2 (2026-07-01) reschedule redesign -- pure policy for the appointment
/// status to persist when an in-place reschedule is finalized. The redesign
/// moves the SAME appointment to the new slot instead of cloning a new row,
/// so the appointment keeps its lifecycle status:
///   - An Approved source sits in
///     <see cref="AppointmentStatusType.RescheduleRequested"/> during the
///     pending window (the submit flow transitions it) and returns to
///     <see cref="AppointmentStatusType.Approved"/> on finalize.
///   - A Pending source stays <see cref="AppointmentStatusType.Pending"/>
///     throughout (B1 skips the Approved-only state-machine step for it).
///
/// The RescheduledNoBill / RescheduledLate outcome is recorded on the
/// change-request row, NOT on the appointment status -- which is why this
/// never returns a terminal Rescheduled* value.
/// </summary>
public static class RescheduleInPlacePolicy
{
    /// <summary>
    /// Returns the appointment status to persist when an in-place reschedule
    /// is approved. <see cref="AppointmentStatusType.RescheduleRequested"/>
    /// resolves back to <see cref="AppointmentStatusType.Approved"/>; every
    /// other status (notably <see cref="AppointmentStatusType.Pending"/>) is
    /// left unchanged so the appointment keeps its current lifecycle state.
    /// </summary>
    public static AppointmentStatusType ResolveFinalizedStatus(AppointmentStatusType current)
    {
        return current == AppointmentStatusType.RescheduleRequested
            ? AppointmentStatusType.Approved
            : current;
    }
}
