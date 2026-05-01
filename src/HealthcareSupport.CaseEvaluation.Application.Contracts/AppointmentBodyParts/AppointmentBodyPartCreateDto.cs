using System;
using System.ComponentModel.DataAnnotations;

namespace HealthcareSupport.CaseEvaluation.AppointmentBodyParts;

public class AppointmentBodyPartCreateDto
{
    public Guid AppointmentInjuryDetailId { get; set; }

    [Required]
    [StringLength(AppointmentBodyPartConsts.BodyPartDescriptionMaxLength)]
    public string BodyPartDescription { get; set; } = null!;
}
