using System;
using System.ComponentModel.DataAnnotations;
using HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails;
using HealthcareSupport.CaseEvaluation.Patients;
using Volo.Abp.Auditing;
using Volo.Abp.Timing;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// 2026-11-06 (R3) -- "I don't know the confirmation number": what a booker knows about the earlier
/// appointment's patient. Every field is kept out of the audit log's stored request parameters, and
/// the request is a POST, so none of it lands in a URL or an access log.
/// </summary>
public class ConfirmationNumberLookupDto
{
    [Required]
    [StringLength(PatientConsts.FirstNameMaxLength)]
    [DisableAuditing]
    public string PatientFirstName { get; set; } = null!;

    [Required]
    [StringLength(PatientConsts.LastNameMaxLength)]
    [DisableAuditing]
    public string PatientLastName { get; set; } = null!;

    /// <summary>A calendar date, so it is not shifted to UTC.</summary>
    [DisableDateTimeNormalization]
    [DisableAuditing]
    public DateTime PatientDateOfBirth { get; set; }

    /// <summary>Any claim number on the earlier appointment; case, spaces and dashes are ignored.</summary>
    [Required]
    [StringLength(AppointmentInjuryDetailConsts.ClaimNumberMaxLength)]
    [DisableAuditing]
    public string ClaimNumber { get; set; } = null!;
}
