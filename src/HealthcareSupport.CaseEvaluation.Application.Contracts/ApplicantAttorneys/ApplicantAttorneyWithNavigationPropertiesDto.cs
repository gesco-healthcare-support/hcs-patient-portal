using HealthcareSupport.CaseEvaluation.States;
using Volo.Abp.Identity;
using System;
using Volo.Abp.Application.Dtos;
using System.Collections.Generic;

namespace HealthcareSupport.CaseEvaluation.ApplicantAttorneys;

public class ApplicantAttorneyWithNavigationPropertiesDto
{
    public ApplicantAttorneyDto ApplicantAttorney { get; set; } = null!;
    public StateDto? State { get; set; }

    public IdentityUserDto IdentityUser { get; set; } = null!;
}