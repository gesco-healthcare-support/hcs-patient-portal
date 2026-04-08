using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace HealthcareSupport.CaseEvaluation.AppointmentStatuses;

public class AppointmentStatusUpdateDto
{
    [Required]
    [StringLength(AppointmentStatusConsts.NameMaxLength)]
    public string Name { get; set; } = null!;
}