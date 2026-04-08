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
    public Patient Patient { get; set; } = null!;
    public IdentityUser IdentityUser { get; set; } = null!;
    public AppointmentType AppointmentType { get; set; } = null!;
    public Location Location { get; set; } = null!;
    public DoctorAvailability DoctorAvailability { get; set; } = null!;
    public AppointmentApplicantAttorneyWithNavigationProperties? AppointmentApplicantAttorney { get; set; }
}