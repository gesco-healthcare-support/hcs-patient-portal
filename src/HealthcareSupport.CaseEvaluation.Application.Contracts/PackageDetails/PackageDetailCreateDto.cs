using System.ComponentModel.DataAnnotations;

namespace HealthcareSupport.CaseEvaluation.PackageDetails;

public class PackageDetailCreateDto
{
    [Required]
    [StringLength(PackageDetailConsts.PackageNameMaxLength)]
    public string PackageName { get; set; } = null!;

    /// <summary>
    /// Required by domain rule -- a package always belongs to one
    /// AppointmentType (PQME / AME / etc.). Schema permits null but the
    /// AppService rejects null per OLD UI contract.
    /// </summary>
    [Required]
    public Guid? AppointmentTypeId { get; set; }

    public bool IsActive { get; set; } = true;
}
