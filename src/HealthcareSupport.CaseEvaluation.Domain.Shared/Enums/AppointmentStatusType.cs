namespace HealthcareSupport.CaseEvaluation.Enums
{
    /// <summary>
    /// OLD-parity base (Phase 1.7, 2026-05-01) extended for the redesign's
    /// 6-status model. <see cref="InfoRequested"/> = 14 is re-introduced
    /// (2026-06-13) for the redesign Send Back / Request-more-information flow;
    /// it had been removed in Phase 0.2 when the original SendBack was deleted.
    /// Legacy values (NoShow, CheckedIn, CheckedOut, Billed, and the
    /// Cancelled/Rescheduled bill variants) are retained for data compatibility
    /// with existing rows; the redesigned UI buckets every value into the six
    /// pills (Pending, Info Requested, Approved, Rejected, Cancelled, Rescheduled).
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
        InfoRequested = 14,

        /// <summary>
        /// Phase 5 (2026-08-07). The patient ARRIVED but was not evaluated --
        /// a missing or incorrect interpreter, the patient leaving before being
        /// called, and similar. Distinct from <see cref="NoShow"/>, where the
        /// patient never arrived at all. Both end with no evaluation performed
        /// and neither produces a replacement appointment: a client who still
        /// wants one submits a new request.
        ///
        /// Recorded by intake staff in the Case Tracker and pushed to the portal,
        /// so the portal never originates this status.
        ///
        /// The value is persisted as its int, so 15 is PERMANENT -- renumbering
        /// would silently relabel stored rows.
        /// </summary>
        NotSeen = 15,
    }
}
