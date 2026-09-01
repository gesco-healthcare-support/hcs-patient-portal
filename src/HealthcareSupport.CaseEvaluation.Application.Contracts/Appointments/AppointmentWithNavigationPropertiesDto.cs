using HealthcareSupport.CaseEvaluation.AppointmentAccessors;
using HealthcareSupport.CaseEvaluation.AppointmentApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentClaimExaminers;
using HealthcareSupport.CaseEvaluation.AppointmentDefenseAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentEmployerDetails;
using HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails;
using HealthcareSupport.CaseEvaluation.AppointmentPrimaryInsurances;
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

    /// <summary>QA F-011 (2026-06-23): the actual booker (Appointment.BookedByUserId,
    /// CreatorId fallback) -- distinct from the patient/owner IdentityUser. Set on
    /// the single-item load; null on list results.</summary>
    public IdentityUserDto? BookedByUser { get; set; }

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

    /// <summary>CI1 (2026-06-05) -- single appointment-level CE + insurance.</summary>
    public AppointmentClaimExaminerDto? ClaimExaminer { get; set; }
    public AppointmentPrimaryInsuranceDto? PrimaryInsurance { get; set; }

    /// <summary>
    /// Phase 4d (2026-08-05) -- set only when this appointment was created by finalizing a
    /// reschedule; null otherwise. Populated on the DETAIL reads only: the "rescheduled from" block
    /// is a detail-page element, so carrying it on every list row would widen each page of the grid
    /// for something nothing renders.
    /// </summary>
    public RescheduleChainDto? RescheduleChain { get; set; }
}