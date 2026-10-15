namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments;

/// <summary>
/// State of a required document that has NOT yet been Accepted, used by the
/// missing-required-documents indicator. Ordered most-actionable first when a
/// required document has several upload rows (see
/// <see cref="RequiredDocumentEvaluator"/>): an Uploaded row (awaiting office
/// review) outranks a Rejected row, which outranks "not uploaded" (only a
/// Pending placeholder, or no row at all). A required document with an Accepted
/// row is satisfied and is not reported at all.
/// </summary>
public enum RequiredDocumentState
{
    NotUploaded = 0,
    AwaitingReview = 1,
    Rejected = 2,
}
