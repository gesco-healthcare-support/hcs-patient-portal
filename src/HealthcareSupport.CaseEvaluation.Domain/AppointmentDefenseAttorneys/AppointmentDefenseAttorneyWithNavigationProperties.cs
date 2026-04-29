using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.DefenseAttorneys;
using Volo.Abp.Identity;
using System;
using System.Collections.Generic;
using HealthcareSupport.CaseEvaluation.AppointmentDefenseAttorneys;

namespace HealthcareSupport.CaseEvaluation.AppointmentDefenseAttorneys;

public class AppointmentDefenseAttorneyWithNavigationProperties
{
    public AppointmentDefenseAttorney AppointmentDefenseAttorney { get; set; } = null!;
    public Appointment? Appointment { get; set; }
    public DefenseAttorney? DefenseAttorney { get; set; }
    public IdentityUser? IdentityUser { get; set; }
}
