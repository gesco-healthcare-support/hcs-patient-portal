using HealthcareSupport.CaseEvaluation.Enums;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// The statuses that end a request, so it no longer blocks a new booking for the same patient, exam
/// type and date/time (2026-11-13). Everything else (Pending, Approved, the two change-request states
/// and InfoRequested) is active.
/// </summary>
public static class AppointmentDuplicateBookingRule
{
    public static readonly AppointmentStatusType[] ClosedStatuses =
    {
        AppointmentStatusType.Rejected,
        AppointmentStatusType.CancelledNoBill,
        AppointmentStatusType.CancelledLate,
        AppointmentStatusType.RescheduledNoBill,
        AppointmentStatusType.RescheduledLate,
        AppointmentStatusType.NoShow,
        AppointmentStatusType.NotSeen,
    };
}
