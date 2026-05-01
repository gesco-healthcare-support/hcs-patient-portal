using HealthcareSupport.CaseEvaluation.States;
using Volo.Abp.Identity;
using System;
using System.Collections.Generic;
using HealthcareSupport.CaseEvaluation.DefenseAttorneys;

namespace HealthcareSupport.CaseEvaluation.DefenseAttorneys;

public class DefenseAttorneyWithNavigationProperties
{
    public DefenseAttorney DefenseAttorney { get; set; } = null!;
    public State? State { get; set; }

    public IdentityUser? IdentityUser { get; set; }
}
