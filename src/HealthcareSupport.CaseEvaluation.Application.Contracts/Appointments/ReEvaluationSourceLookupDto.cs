using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Auditing;
using Volo.Abp.Timing;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// 2026-11-06 -- find the earlier appointment a re-evaluation follows from, by its confirmation
/// number and the patient's date of birth. Sent in a POST body, never a query string, so neither
/// value lands in a URL or an access log.
/// </summary>
public class ReEvaluationSourceLookupDto
{
    [Required]
    [StringLength(AppointmentConsts.RequestConfirmationNumberMaxLength)]
    public string ConfirmationNumber { get; set; } = null!;

    /// <summary>
    /// The patient's date of birth. A calendar date, so it is not shifted to UTC; kept out of the
    /// audit log's stored request parameters.
    ///
    /// <para>2026-11-08 (W2): optional. Every re-evaluation load comes through this lookup now, and a
    /// party to the newest case in the chain needs no date; only a caller who is not must give one.</para>
    /// </summary>
    [DisableDateTimeNormalization]
    [DisableAuditing]
    public DateTime? PatientDateOfBirth { get; set; }
}
