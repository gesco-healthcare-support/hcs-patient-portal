using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace HealthcareSupport.CaseEvaluation.AppointmentStatuses;

public class AppointmentStatusCreateDto
{
    [Required]
    [StringLength(AppointmentStatusConsts.NameMaxLength)]
    public string Name { get; set; } = null!;
}