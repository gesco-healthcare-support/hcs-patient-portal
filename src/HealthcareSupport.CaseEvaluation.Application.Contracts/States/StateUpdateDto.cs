using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities;

namespace HealthcareSupport.CaseEvaluation.States;

public class StateUpdateDto : IHasConcurrencyStamp
{
    [Required]
    public string Name { get; set; } = null!;
    public string ConcurrencyStamp { get; set; } = null!;
}