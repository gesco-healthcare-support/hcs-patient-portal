using System.ComponentModel.DataAnnotations;

namespace HealthcareSupport.CaseEvaluation.PackageDetails;

/// <summary>
/// IT Admin can rename a package, change its target AppointmentType, or
/// flip IsActive. Linked DocumentPackages are managed separately via the
/// LinkDocumentsAsync / UnlinkDocumentAsync endpoints so the package update
/// path stays focused on package-level fields only.
/// </summary>
public class PackageDetailUpdateDto
{
    [Required]
    [StringLength(PackageDetailConsts.PackageNameMaxLength)]
    public string PackageName { get; set; } = null!;

    [Required]
    public Guid? AppointmentTypeId { get; set; }

    public bool IsActive { get; set; }
}
