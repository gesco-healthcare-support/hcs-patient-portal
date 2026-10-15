namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments;

/// <summary>
/// G-03 (PR3): result of the missing-required-documents read. <see cref="RequiredCount"/>
/// is how many documents the active package template(s) require for the appointment's
/// type (0 when the type has no active package or none are active); <see cref="Missing"/>
/// lists those required documents not yet Accepted. The indicator renders nothing when
/// RequiredCount is 0, a positive "all received" banner when RequiredCount &gt; 0 and
/// Missing is empty, otherwise the outstanding list.
/// </summary>
public class MissingRequiredDocumentsResultDto
{
    public int RequiredCount { get; set; }

    public List<MissingRequiredDocumentDto> Missing { get; set; } = new();
}
