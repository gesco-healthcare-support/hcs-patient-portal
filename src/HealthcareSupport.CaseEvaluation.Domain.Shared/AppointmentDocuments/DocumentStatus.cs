namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments;

/// <summary>
/// Per-document review state. Mirrors OLD's DocumentStatuses enum verbatim
/// for strict parity (Phase 1.7, 2026-05-01):
///   Uploaded=1 / Accepted=2 / Rejected=3 / Pending=4
/// OLD also has Deleted=5; NEW uses ABP's <c>ISoftDelete</c> filter for that
/// concern (acceptable framework deviation; row removal semantics match).
///
/// Internal users' uploads land directly as Accepted.
/// External users' uploads land as Uploaded pending office review.
/// Pending is the "queued, awaiting upload" state used when the office
/// auto-creates package-document rows on appointment approval.
/// Clinic Staff / Staff Supervisor / IT Admin accept (-> Accepted) or
/// reject (-> Rejected with RejectionReason).
/// </summary>
public enum DocumentStatus
{
    Uploaded = 1,
    Accepted = 2,
    Rejected = 3,
    Pending = 4,
}
