using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace HealthcareSupport.CaseEvaluation.AppointmentTypes;

public class AppointmentTypeDto : FullAuditedEntityDto<Guid>
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }

    /// <summary>Reserved built-in type: surfaced read-only (no edit/delete).</summary>
    public bool IsSystem { get; set; }

    /// <summary>Number of Appointment rows referencing this type. Null means
    /// "not tracked" (e.g. single-row reads that do not compute it).</summary>
    public int? UsageCount { get; set; }
}