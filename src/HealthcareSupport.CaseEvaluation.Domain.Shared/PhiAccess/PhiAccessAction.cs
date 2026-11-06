namespace HealthcareSupport.CaseEvaluation.PhiAccess;

/// <summary>
/// What a caller did with protected health information, as recorded in the PHI access log.
/// Stored as an integer, so every value is pinned: never renumber or reuse one, only add new ones
/// (see <c>PhiAccessActionContractTests</c>).
/// </summary>
public enum PhiAccessAction
{
    /// <summary>Opened one patient record.</summary>
    PatientViewed = 1,

    /// <summary>Revealed a patient's full Social Security number.</summary>
    PatientSsnRevealed = 2,

    /// <summary>Opened one appointment, which carries the patient's record.</summary>
    AppointmentViewed = 3,

    /// <summary>Downloaded an uploaded appointment document.</summary>
    DocumentDownloaded = 4,

    /// <summary>Downloaded a generated appointment packet.</summary>
    PacketDownloaded = 5,

    /// <summary>Exported the appointment report as a PDF. One row per appointment in it.</summary>
    ReportPdfExported = 6,

    /// <summary>Exported the appointment report as a CSV. One row per appointment in it.</summary>
    ReportCsvExported = 7,

    /// <summary>Exported one appointment's demographics sheet as a PDF.</summary>
    DemographicsExported = 8,

    /// <summary>
    /// Exported selected rows of the internal appointment list as a CSV (built in the browser,
    /// recorded by the server first). One row per appointment in it.
    /// </summary>
    AppointmentListCsvExported = 9,

    /// <summary>
    /// Loaded an earlier appointment to start a re-evaluation by its confirmation number and the
    /// patient's date of birth, as someone who is NOT a party to it (2026-11-06). A party loading
    /// its own case is recorded as <see cref="AppointmentViewed"/>.
    /// </summary>
    ReEvaluationSourceLoadedByDateOfBirth = 10,

    /// <summary>
    /// A re-evaluation lookup by confirmation number and date of birth that loaded nothing: no such
    /// number, a wrong date of birth, or a case that cannot be re-evaluated (2026-11-06). Recorded
    /// with no patient or appointment, because the refusal must not say which one was meant.
    /// </summary>
    ReEvaluationSourceLookupRefused = 11,
}
