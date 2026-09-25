using HealthcareSupport.CaseEvaluation.States;
using HealthcareSupport.CaseEvaluation.AppointmentLanguages;
using Volo.Abp.Identity;
using Volo.Saas.Host.Dtos;
using System;
using Volo.Abp.Application.Dtos;
using System.Collections.Generic;

namespace HealthcareSupport.CaseEvaluation.Patients;

public class PatientWithNavigationPropertiesDto
{
    public PatientDto Patient { get; set; } = null!;
    public StateDto? State { get; set; }

    public AppointmentLanguageDto? AppointmentLanguage { get; set; }

    public IdentityUserDto? IdentityUser { get; set; }
    public SaasTenantDto? Tenant { get; set; }

    /// <summary>
    /// R2 (Phase 9, 2026-05-04) -- true when this Patient was resolved to an
    /// already-existing row (email match, 3-of-6 dedup match, or
    /// FindOrCreate.wasFound=true), false when a brand-new row was just
    /// created by <c>GetOrCreatePatientForAppointmentBookingAsync</c>.
    /// The Angular booking form must echo this value into
    /// <c>AppointmentCreateDto.IsPatientAlreadyExist</c> on the subsequent
    /// create call so the appointment row carries the OLD-parity flag
    /// (P:\PatientPortalOld\PatientAppointment.Domain\Core\AppointmentDomain.cs:210, 217).
    /// Server-computed; ignored by the Mapperly mapper.
    /// </summary>
    public bool IsExisting { get; set; }
}