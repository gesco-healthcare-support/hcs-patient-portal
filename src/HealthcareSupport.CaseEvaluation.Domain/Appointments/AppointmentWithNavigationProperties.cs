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

    /// <summary>
    /// Phase 13b (2026-05-04) -- defense-attorney link with full nav
    /// payload. Mirrors how the applicant-attorney link is bundled.
    /// </summary>
    public AppointmentDefenseAttorneyWithNavigationProperties? AppointmentDefenseAttorney { get; set; }

    /// <summary>
    /// Phase 13b (2026-05-04) -- 1:1 employer-detail row (NEW current
    /// schema). The audit doc flags 1:N as the OLD-parity intent;
    /// when the schema lifts to 1:N this becomes a List.
    /// </summary>
    public AppointmentEmployerDetailWithNavigationProperties? AppointmentEmployerDetail { get; set; }

    /// <summary>
    /// Phase 13b (2026-05-04) -- 1:N injury-detail rows. Each carries
    /// its sub-entities (BodyParts, ClaimExaminer, PrimaryInsurance,
    /// WcabOffice) per <see cref="AppointmentInjuryDetailWithNavigationProperties"/>.
    /// Mirrors OLD <c>AppointmentDomain.Get(id)</c>'s per-injury sub-fetch
    /// loop at <c>AppointmentDomain.cs:80-95</c>.
    /// </summary>
    public List<AppointmentInjuryDetailWithNavigationProperties> AppointmentInjuryDetails { get; set; } = new();

    /// <summary>
    /// Phase 13b (2026-05-04) -- accessor grants on the appointment.
    /// Surfaces both for the access-policy check and for the UI's
    /// "shared with" panel.
    /// </summary>
    public List<AppointmentAccessor> AppointmentAccessors { get; set; } = new();
}