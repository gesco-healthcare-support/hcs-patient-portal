namespace HealthcareSupport.CaseEvaluation.Enums
{
    /// <summary>
    /// Mirrors OLD's <c>RequestStatus</c> enum verbatim for strict parity
    /// (Phase 1.7, 2026-05-01). Used by <c>AppointmentChangeRequest</c> to
    /// track cancel / reschedule submission lifecycle. Numeric values match
    /// OLD's centralized enum table (gap in IDs is OLD-faithful).
    /// </summary>
    public enum RequestStatusType
    {
        Pending = 25,
        Accepted = 26,
        Rejected = 27,
    }
}
