namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments;

/// <summary>
/// W2-11: per-document review state. Mirrors OLD's DocumentStatuses enum
/// (Uploaded=1 / Accepted=2 / Rejected=3 / Pending=4 / Deleted=5) but
/// renames Accepted -> Approved for symmetry with the appointment-status
/// vocabulary, and drops Pending + Deleted (Pending is unused in OLD; soft
/// delete uses ABP's IsDeleted).
///
/// Internal users' uploads land directly as Approved.
/// External users' uploads land as Uploaded pending office review.
/// Office Admin / Practice Admin / Supervisor approve (-> Approved) or
/// reject (-> Rejected with RejectionReason).
/// </summary>
public enum DocumentStatus
{
    Uploaded = 1,
    Approved = 2,
    Rejected = 3,
}
