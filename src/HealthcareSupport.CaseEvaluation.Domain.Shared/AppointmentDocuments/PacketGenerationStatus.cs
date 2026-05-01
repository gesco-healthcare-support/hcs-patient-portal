namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments;

/// <summary>
/// W2-11: state of the per-appointment PDF packet job.
///   Generating  - the Hangfire job is in flight (UI shows a spinner).
///   Generated   - PDF blob is ready; UI shows a Download button.
///   Failed      - the job caught a PdfSharp / IO exception; UI surfaces
///                 the error message and a Regenerate button.
/// </summary>
public enum PacketGenerationStatus
{
    Generating = 1,
    Generated = 2,
    Failed = 3,
}
