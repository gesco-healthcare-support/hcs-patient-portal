using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Entities;

namespace HealthcareSupport.CaseEvaluation.States;

public class StateDto : FullAuditedEntityDto<Guid>, IHasConcurrencyStamp
{
    public string Name { get; set; } = null!;
    public string ConcurrencyStamp { get; set; } = null!;

    /// <summary>Reserved built-in state: surfaced read-only (no edit/delete).</summary>
    public bool IsSystem { get; set; }

    /// <summary>Total references across Location / WcabOffice / Patient /
    /// ApplicantAttorney / DefenseAttorney / ClaimExaminer. Null means "not
    /// tracked" (e.g. single-row reads that do not compute it).</summary>
    public int? UsageCount { get; set; }
}