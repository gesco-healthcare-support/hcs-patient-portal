using HealthcareSupport.CaseEvaluation.States;
using Volo.Abp.Identity;
using System;
using System.Collections.Generic;
using HealthcareSupport.CaseEvaluation.ApplicantAttorneys;

namespace HealthcareSupport.CaseEvaluation.ApplicantAttorneys;

public class ApplicantAttorneyWithNavigationProperties
{
    public ApplicantAttorney ApplicantAttorney { get; set; } = null!;
    public State? State { get; set; }

    public IdentityUser? IdentityUser { get; set; }
}