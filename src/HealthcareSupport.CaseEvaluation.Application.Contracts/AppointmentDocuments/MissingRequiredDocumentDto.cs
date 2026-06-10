namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments;

/// <summary>
/// G-03 (PR3): a required document still outstanding for an appointment, for the
/// missing-required-documents indicator. <see cref="DocumentId"/> is the internal
/// master Document id (never displayed); the UI shows <see cref="Name"/> and
/// styles by <see cref="State"/>. A required document that already has an
/// Accepted upload is satisfied and never appears here.
/// </summary>
public class MissingRequiredDocumentDto
{
    public Guid DocumentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public RequiredDocumentState State { get; set; }
}
