using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace HealthcareSupport.CaseEvaluation.AppointmentLanguages;

public class AppointmentLanguageCreateDto
{
    [Required]
    [StringLength(AppointmentLanguageConsts.NameMaxLength)]
    public string Name { get; set; } = "English";
}