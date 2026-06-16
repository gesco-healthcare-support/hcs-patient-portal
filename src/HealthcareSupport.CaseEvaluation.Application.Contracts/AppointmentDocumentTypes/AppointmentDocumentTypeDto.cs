using System;
using Volo.Abp.Application.Dtos;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocumentTypes;

/// <summary>
/// Read model for a document-category row. <see cref="IsSystem"/> rows
/// (e.g. "Generated Packet") are surfaced to the admin list read-only --
/// the SPA disables their edit/delete actions; the upload picker (later
/// slice) hides them entirely.
/// </summary>
public class AppointmentDocumentTypeDto : FullAuditedEntityDto<Guid>
{
    public string Name { get; set; } = null!;

    /// <summary>The appointment type this category is scoped to; null means it
    /// applies to every appointment type (used by the reserved system row).</summary>
    public Guid? AppointmentTypeId { get; set; }

    public bool IsSystem { get; set; }

    public bool IsActive { get; set; }

    /// <summary>Number of AppointmentDocument rows referencing this type. Null
    /// means "not tracked" (e.g. single-row reads that do not compute it).</summary>
    public int? UsageCount { get; set; }
}
