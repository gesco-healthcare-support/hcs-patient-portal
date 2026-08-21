using Volo.Abp.Application.Dtos;

namespace HealthcareSupport.CaseEvaluation.CustomFields;

public class GetCustomFieldsInput : PagedAndSortedResultRequestDto
{
    public string? FilterText { get; set; }
    public Guid? AppointmentTypeId { get; set; }
    public bool? IsActive { get; set; }
}
