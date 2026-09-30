using System;
using Volo.Abp.Application.Dtos;

namespace HealthcareSupport.CaseEvaluation.NotificationTemplates;

/// <summary>
/// Read DTO for the host-scoped <c>NotificationTemplateType</c> lookup
/// (Email / SMS). Surfaced as a navigation property on the With-Nav DTO
/// so the editor UI can render the "Email" / "SMS" badge alongside the
/// template Subject + body without a second round-trip.
/// </summary>
public class NotificationTemplateTypeDto : FullAuditedEntityDto<Guid>
{
    public string Name { get; set; } = null!;
    public bool IsActive { get; set; }
}
