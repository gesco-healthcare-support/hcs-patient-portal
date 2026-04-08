using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.ApplicantAttorneys;
using Volo.Abp.Identity;
using System;
using Volo.Abp.Application.Dtos;
using System.Collections.Generic;

namespace HealthcareSupport.CaseEvaluation.AppointmentApplicantAttorneys;

public class AppointmentApplicantAttorneyWithNavigationPropertiesDto
{
    public AppointmentApplicantAttorneyDto AppointmentApplicantAttorney { get; set; } = null!;
    public AppointmentDto Appointment { get; set; } = null!;
    public ApplicantAttorneyDto ApplicantAttorney { get; set; } = null!;
    public IdentityUserDto IdentityUser { get; set; } = null!;
}