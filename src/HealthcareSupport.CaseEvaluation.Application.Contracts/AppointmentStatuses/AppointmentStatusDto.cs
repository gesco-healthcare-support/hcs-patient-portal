using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace HealthcareSupport.CaseEvaluation.AppointmentStatuses;

public class AppointmentStatusDto : FullAuditedEntityDto<Guid>
{
    public string Name { get; set; } = null!;

    /// <summary>Reserved built-in status: surfaced read-only (no edit/delete).</summary>
    public bool IsSystem { get; set; }

    /// <summary>Always null: the status lookup is not FK-referenced (appointments
    /// use the AppointmentStatusType enum), so usage is not tracked.</summary>
    public int? UsageCount { get; set; }
}