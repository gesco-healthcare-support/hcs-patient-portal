using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace HealthcareSupport.CaseEvaluation.States;

public class StateCreateDto
{
    [Required]
    public string Name { get; set; } = null!;
}