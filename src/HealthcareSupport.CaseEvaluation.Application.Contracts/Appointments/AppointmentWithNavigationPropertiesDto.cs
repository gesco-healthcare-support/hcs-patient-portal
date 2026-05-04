using HealthcareSupport.CaseEvaluation.AppointmentAccessors;
using HealthcareSupport.CaseEvaluation.AppointmentApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentDefenseAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentEmployerDetails;
using HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails;
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

    /// <summary>Phase 13b (2026-05-04).</summary>
    public AppointmentDefenseAttorneyWithNavigationPropertiesDto? AppointmentDefenseAttorney { get; set; }

    /// <summary>Phase 13b (2026-05-04).</summary>
    public AppointmentEmployerDetailWithNavigationPropertiesDto? AppointmentEmployerDetail { get; set; }

    /// <summary>Phase 13b (2026-05-04).</summary>
    public List<AppointmentInjuryDetailWithNavigationPropertiesDto> AppointmentInjuryDetails { get; set; } = new();

    /// <summary>Phase 13b (2026-05-04).</summary>
    public List<AppointmentAccessorDto> AppointmentAccessors { get; set; } = new();
}