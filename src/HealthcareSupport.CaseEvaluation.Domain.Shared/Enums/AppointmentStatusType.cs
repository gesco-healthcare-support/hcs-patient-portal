namespace HealthcareSupport.CaseEvaluation.Enums
{
    /// <summary>
    /// Mirrors OLD's AppointmentStatusType enum verbatim for strict parity
    /// (Phase 1.7, 2026-05-01). 13 values; the NEW-only AwaitingMoreInfo=14
    /// state was removed when the SendBack flow was deleted in Phase 0.2.
    /// </summary>
    public enum AppointmentStatusType
    {
        Pending = 1,
        Approved = 2,
        Rejected = 3,
        NoShow = 4,
        CancelledNoBill = 5,
        CancelledLate = 6,
        RescheduledNoBill = 7,
        RescheduledLate = 8,
        CheckedIn = 9,
        CheckedOut = 10,
        Billed = 11,
        RescheduleRequested = 12,
        CancellationRequested = 13,
    }
}
