using HealthcareSupport.CaseEvaluation.AppointmentApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.Patients;
using Volo.Abp.Identity;
using HealthcareSupport.CaseEvaluation.AppointmentTypes;
using HealthcareSupport.CaseEvaluation.Locations;
using HealthcareSupport.CaseEvaluation.DoctorAvailabilities;
using System;
using Volo.Abp.Application.Dtos;
using System.Collections.Generic;

namespace HealthcareSupport.CaseEvaluation.Appointments;

public class AppointmentWithNavigationPropertiesDto
{
    public AppointmentDto Appointment { get; set; } = null!;
    public PatientDto Patient { get; set; } = null!;
    public IdentityUserDto IdentityUser { get; set; } = null!;
    public AppointmentTypeDto AppointmentType { get; set; } = null!;
    public LocationDto Location { get; set; } = null!;
    public DoctorAvailabilityDto DoctorAvailability { get; set; } = null!;
    public AppointmentApplicantAttorneyWithNavigationPropertiesDto? AppointmentApplicantAttorney { get; set; }
}