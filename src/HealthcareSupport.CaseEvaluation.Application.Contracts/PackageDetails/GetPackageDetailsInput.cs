using Volo.Abp.Application.Dtos;

namespace HealthcareSupport.CaseEvaluation.PackageDetails;

public class GetPackageDetailsInput : PagedAndSortedResultRequestDto
{
    public string? FilterText { get; set; }
    public Guid? AppointmentTypeId { get; set; }
    public bool? IsActive { get; set; }
}
