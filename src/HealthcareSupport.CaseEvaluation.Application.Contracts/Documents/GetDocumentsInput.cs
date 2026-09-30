using Volo.Abp.Application.Dtos;

namespace HealthcareSupport.CaseEvaluation.Documents;

/// <summary>
/// Filter / paging input for the master Documents listing. <c>FilterText</c>
/// matches against Name (case-insensitive contains). <c>IsActive</c> null
/// means "any status" so deactivated rows can still be reviewed by IT Admin
/// without forcing a separate switch.
/// </summary>
public class GetDocumentsInput : PagedAndSortedResultRequestDto
{
    public string? FilterText { get; set; }
    public bool? IsActive { get; set; }
}
