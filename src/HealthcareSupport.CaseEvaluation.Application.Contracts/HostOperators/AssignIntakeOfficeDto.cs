using System;
using System.ComponentModel.DataAnnotations;

namespace HealthcareSupport.CaseEvaluation.HostOperators;

/// <summary>
/// Phase D (2026-06-25) -- request to assign a host Intake operator to an
/// office. Assigning eagerly provisions the operator's limited shadow Intake
/// user in that office's database (O-D3).
/// </summary>
public class AssignIntakeOfficeDto
{
    [Required]
    public Guid OperatorUserId { get; set; }

    [Required]
    public Guid OfficeId { get; set; }
}
