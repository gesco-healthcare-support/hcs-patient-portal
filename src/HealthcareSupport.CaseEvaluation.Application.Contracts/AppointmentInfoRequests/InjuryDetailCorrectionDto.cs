using System;
using System.ComponentModel.DataAnnotations;
using HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails;

namespace HealthcareSupport.CaseEvaluation.AppointmentInfoRequests;

/// <summary>
/// One corrected Claim Information (injury-detail) row supplied by the external party on
/// the fix-it page (QA item 11, 2026-07-01). Claim Information is a repeating collection,
/// not a scalar registry field, so the fix-it flow carries the whole set as a replacement
/// list on <see cref="SaveInfoRequestCorrectionsInput.InjuryDetails"/> rather than through
/// the key/value <c>Corrections</c> map. The AppointmentId is the corrections route param,
/// so it is intentionally absent here. Field constraints mirror
/// <see cref="AppointmentInjuryDetailCreateDto"/> and the domain ctor guards.
/// </summary>
public class InjuryDetailCorrectionDto
{
    [Required]
    public DateTime DateOfInjury { get; set; }

    public DateTime? ToDateOfInjury { get; set; }

    [Required]
    [StringLength(AppointmentInjuryDetailConsts.ClaimNumberMaxLength)]
    public string ClaimNumber { get; set; } = null!;

    public bool IsCumulativeInjury { get; set; }

    [Required]
    [StringLength(AppointmentInjuryDetailConsts.WcabAdjMaxLength)]
    public string WcabAdj { get; set; } = null!;

    [Required]
    [StringLength(AppointmentInjuryDetailConsts.BodyPartsSummaryMaxLength)]
    public string BodyPartsSummary { get; set; } = null!;

    public Guid? WcabOfficeId { get; set; }
}
