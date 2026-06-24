using HealthcareSupport.CaseEvaluation.Documents;

namespace HealthcareSupport.CaseEvaluation.PackageDetails;

/// <summary>
/// Read-with-navigation-properties projection: package metadata plus the
/// list of linked Documents. Used by the IT Admin UI when editing a package
/// so the user sees the current contents without a second round-trip.
/// </summary>
public class PackageDetailWithDocumentsDto
{
    public PackageDetailDto Package { get; set; } = null!;
    public List<DocumentDto> LinkedDocuments { get; set; } = new();
}
