using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace HealthcareSupport.CaseEvaluation.AppointmentLanguages;

public class AppointmentLanguageDto : FullAuditedEntityDto<Guid>
{
    public string Name { get; set; } = null!;

    /// <summary>Reserved built-in language: surfaced read-only (no edit/delete).</summary>
    public bool IsSystem { get; set; }

    /// <summary>Number of Patient rows referencing this language. Null means
    /// "not tracked" (e.g. single-row reads that do not compute it).</summary>
    public int? UsageCount { get; set; }
}