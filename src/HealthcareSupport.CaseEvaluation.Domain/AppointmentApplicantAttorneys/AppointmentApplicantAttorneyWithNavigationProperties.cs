using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.ApplicantAttorneys;
using Volo.Abp.Identity;
using System;
using System.Collections.Generic;
using HealthcareSupport.CaseEvaluation.AppointmentApplicantAttorneys;

namespace HealthcareSupport.CaseEvaluation.AppointmentApplicantAttorneys;

public class AppointmentApplicantAttorneyWithNavigationProperties
{
    public AppointmentApplicantAttorney AppointmentApplicantAttorney { get; set; } = null!;
    public Appointment Appointment { get; set; } = null!;
    public ApplicantAttorney ApplicantAttorney { get; set; } = null!;
    public IdentityUser IdentityUser { get; set; } = null!;
}