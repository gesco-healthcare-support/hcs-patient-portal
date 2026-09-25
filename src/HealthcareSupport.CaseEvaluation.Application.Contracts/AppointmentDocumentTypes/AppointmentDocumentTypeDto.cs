using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocumentTypes;

/// <summary>
/// Read model for a document-category row. <see cref="IsSystem"/> rows
/// (e.g. "Generated Packet") are surfaced to the admin list read-only --
/// the SPA disables their edit/delete actions; the upload picker hides them
/// entirely.
/// </summary>
public class AppointmentDocumentTypeDto : FullAuditedEntityDto<Guid>
{
    public string Name { get; set; } = null!;

    /// <summary>The appointment types this category is offered for (#4 M2M).</summary>
    public List<Guid> AppointmentTypeIds { get; set; } = new();

    /// <summary>True when the category is offered for every appointment type
    /// (used by the reserved system row).</summary>
    public bool AppliesToAll { get; set; }

    public bool IsSystem { get; set; }

    public bool IsActive { get; set; }

    /// <summary>Number of AppointmentDocument rows referencing this type. Null
    /// means "not tracked" (e.g. single-row reads that do not compute it).</summary>
    public int? UsageCount { get; set; }
}
