using HealthcareSupport.CaseEvaluation.AppointmentApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.Patients;
using Volo.Abp.Identity;
using HealthcareSupport.CaseEvaluation.AppointmentTypes;
using HealthcareSupport.CaseEvaluation.Locations;
using HealthcareSupport.CaseEvaluation.DoctorAvailabilities;
using System;
using System.Collections.Generic;
using HealthcareSupport.CaseEvaluation.Appointments;

namespace HealthcareSupport.CaseEvaluation.Appointments;

public class AppointmentWithNavigationProperties
{
    public Appointment Appointment { get; set; } = null!;
    public Patient? Patient { get; set; }
    public IdentityUser? IdentityUser { get; set; }
    public AppointmentType? AppointmentType { get; set; }
    public Location? Location { get; set; }
    public DoctorAvailability? DoctorAvailability { get; set; }
    public AppointmentApplicantAttorneyWithNavigationProperties? AppointmentApplicantAttorney { get; set; }
}