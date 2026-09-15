using HealthcareSupport.CaseEvaluation.States;
using Volo.Abp.Identity;

namespace HealthcareSupport.CaseEvaluation.ClaimExaminers;

public class ClaimExaminerWithNavigationPropertiesDto
{
    public ClaimExaminerDto ClaimExaminer { get; set; } = null!;
    public StateDto? State { get; set; }

    public IdentityUserDto? IdentityUser { get; set; }
}
