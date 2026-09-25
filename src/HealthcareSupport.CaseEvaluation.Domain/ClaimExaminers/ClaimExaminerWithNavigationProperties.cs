using System;
using HealthcareSupport.CaseEvaluation.States;
using Volo.Abp.Identity;

namespace HealthcareSupport.CaseEvaluation.ClaimExaminers;

public class ClaimExaminerWithNavigationProperties
{
    public ClaimExaminer ClaimExaminer { get; set; } = null!;
    public State? State { get; set; }

    public IdentityUser? IdentityUser { get; set; }
}
