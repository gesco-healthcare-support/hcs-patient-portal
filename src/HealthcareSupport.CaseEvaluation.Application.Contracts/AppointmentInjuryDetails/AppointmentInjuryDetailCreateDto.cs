using System;
using System.ComponentModel.DataAnnotations;

namespace HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails;

public class AppointmentInjuryDetailCreateDto
{
    public Guid AppointmentId { get; set; }

    [Required]
    public DateTime DateOfInjury { get; set; }

    public DateTime? ToDateOfInjury { get; set; }

    [Required]
    [StringLength(AppointmentInjuryDetailConsts.ClaimNumberMaxLength)]
    public string ClaimNumber { get; set; } = null!;

    [Required]
    public bool IsCumulativeInjury { get; set; }

    [StringLength(AppointmentInjuryDetailConsts.WcabAdjMaxLength)]
    public string? WcabAdj { get; set; }

    [Required]
    [StringLength(AppointmentInjuryDetailConsts.BodyPartsSummaryMaxLength)]
    public string BodyPartsSummary { get; set; } = null!;

    public Guid? WcabOfficeId { get; set; }
}
