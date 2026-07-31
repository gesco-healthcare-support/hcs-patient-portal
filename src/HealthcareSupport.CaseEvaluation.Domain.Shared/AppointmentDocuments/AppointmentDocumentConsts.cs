namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments;

public static class AppointmentDocumentConsts
{
    public const int FileNameMaxLength = 255;
    public const int ContentTypeMaxLength = 100;
    public const int DocumentNameMaxLength = 200;
    public const int BlobNameMaxLength = 100;

    /// <summary>G-03-03: free-text label captured when the uploader picks the
    /// "Other" document-type option. Matches AppointmentDocumentType.NameMaxLength.</summary>
    public const int OtherDocumentTypeNameMaxLength = 100;

    /// <summary>
    /// AF7 / BUG-025 (2026-06-05) -- single source of truth for the per-document
    /// upload cap, 10 MB. The Application <c>AppointmentDocumentsAppService</c>
    /// cap aliases this, the Domain <c>AppointmentDocumentManager</c> rejects
    /// against it, and the Angular client mirrors the same value, so the
    /// friendly "too large" message fires client-side before the 12 MB
    /// Kestrel/multipart framework cap returns a raw 413.
    /// </summary>
    public const long MaxFileSizeBytes = 10L * 1024L * 1024L;
}
