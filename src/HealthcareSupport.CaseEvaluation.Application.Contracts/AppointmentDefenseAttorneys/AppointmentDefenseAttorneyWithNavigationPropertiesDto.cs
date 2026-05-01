using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.DefenseAttorneys;
using Volo.Abp.Identity;
using System;
using Volo.Abp.Application.Dtos;
using System.Collections.Generic;

namespace HealthcareSupport.CaseEvaluation.AppointmentDefenseAttorneys;

public class AppointmentDefenseAttorneyWithNavigationPropertiesDto
{
    public AppointmentDefenseAttorneyDto AppointmentDefenseAttorney { get; set; } = null!;
    public AppointmentDto? Appointment { get; set; }
    public DefenseAttorneyDto? DefenseAttorney { get; set; }
    public IdentityUserDto? IdentityUser { get; set; }
}
