using HealthcareSupport.CaseEvaluation.States;
using Volo.Abp.Identity;
using System;
using Volo.Abp.Application.Dtos;
using System.Collections.Generic;

namespace HealthcareSupport.CaseEvaluation.DefenseAttorneys;

public class DefenseAttorneyWithNavigationPropertiesDto
{
    public DefenseAttorneyDto DefenseAttorney { get; set; } = null!;
    public StateDto? State { get; set; }

    public IdentityUserDto? IdentityUser { get; set; }
}
