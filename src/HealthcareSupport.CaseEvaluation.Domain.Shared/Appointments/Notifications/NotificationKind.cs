namespace HealthcareSupport.CaseEvaluation.Appointments.Notifications;

/// <summary>
/// W2-10: per-event semantics tag passed to the recipient resolver so it
/// can emit different recipient sets per event (e.g. <c>OfficeAdmin</c>
/// receives Submitted notifications; <c>Patient</c> doesn't unless they
/// are also the booker). Also drives subject/body template selection.
/// </summary>
public enum NotificationKind
{
    Submitted = 1,
    Approved = 2,
    Rejected = 3,
    AwaitingMoreInfo = 4,
    RequestSchedulingReminder = 5,
    CancellationRescheduleReminder = 6,
    AppointmentDayReminder = 7,
}
