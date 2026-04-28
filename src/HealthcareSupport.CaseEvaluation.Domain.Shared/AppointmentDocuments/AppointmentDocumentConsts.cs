namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments;

public static class AppointmentDocumentConsts
{
    public const int FileNameMaxLength = 255;
    public const int ContentTypeMaxLength = 100;
    public const int DocumentNameMaxLength = 200;
    public const int BlobNameMaxLength = 100;

    /// <summary>25 MB upload cap. Matches typical clinical PDF bundle sizes.</summary>
    public const long MaxFileSizeBytes = 25L * 1024L * 1024L;
}
