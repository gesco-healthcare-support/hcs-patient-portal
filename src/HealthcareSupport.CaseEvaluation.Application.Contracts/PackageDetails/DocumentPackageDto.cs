namespace HealthcareSupport.CaseEvaluation.PackageDetails;

/// <summary>
/// Surface representation of a row in the M:N link table between
/// PackageDetail and Document. Composite key (PackageDetailId, DocumentId)
/// matches the entity's <c>GetKeys()</c> contract.
/// </summary>
public class DocumentPackageDto
{
    public Guid PackageDetailId { get; set; }
    public Guid DocumentId { get; set; }
    public bool IsActive { get; set; }
}
