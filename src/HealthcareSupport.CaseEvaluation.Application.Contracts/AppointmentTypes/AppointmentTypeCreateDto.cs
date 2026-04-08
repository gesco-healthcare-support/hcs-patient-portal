using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace HealthcareSupport.CaseEvaluation.AppointmentTypes;

public class AppointmentTypeCreateDto
{
    [Required]
    [StringLength(AppointmentTypeConsts.NameMaxLength)]
    public string Name { get; set; } = null!;
    [StringLength(AppointmentTypeConsts.DescriptionMaxLength)]
    public string? Description { get; set; }
}