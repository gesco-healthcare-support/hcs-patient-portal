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
    public PatientDto? Patient { get; set; }
    public IdentityUserDto? IdentityUser { get; set; }
    public AppointmentTypeDto? AppointmentType { get; set; }
    public LocationDto? Location { get; set; }
    public DoctorAvailabilityDto? DoctorAvailability { get; set; }
    public AppointmentApplicantAttorneyWithNavigationPropertiesDto? AppointmentApplicantAttorney { get; set; }
}