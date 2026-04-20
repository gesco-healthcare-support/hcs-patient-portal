using HealthcareSupport.CaseEvaluation.States;
using HealthcareSupport.CaseEvaluation.AppointmentLanguages;
using Volo.Abp.Identity;
using Volo.Saas.Host.Dtos;
using System;
using Volo.Abp.Application.Dtos;
using System.Collections.Generic;

namespace HealthcareSupport.CaseEvaluation.Patients;

public class PatientWithNavigationPropertiesDto
{
    public PatientDto Patient { get; set; } = null!;
    public StateDto? State { get; set; }

    public AppointmentLanguageDto? AppointmentLanguage { get; set; }

    public IdentityUserDto? IdentityUser { get; set; }
    public SaasTenantDto? Tenant { get; set; }
}