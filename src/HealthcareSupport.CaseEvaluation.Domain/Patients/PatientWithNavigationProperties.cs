using HealthcareSupport.CaseEvaluation.States;
using HealthcareSupport.CaseEvaluation.AppointmentLanguages;
using Volo.Abp.Identity;
using Volo.Saas.Tenants;
using System;
using System.Collections.Generic;
using HealthcareSupport.CaseEvaluation.Patients;

namespace HealthcareSupport.CaseEvaluation.Patients;

public class PatientWithNavigationProperties
{
    public Patient Patient { get; set; } = null!;
    public State? State { get; set; }

    public AppointmentLanguage? AppointmentLanguage { get; set; }

    public IdentityUser IdentityUser { get; set; } = null!;
    public Tenant? Tenant { get; set; }
}