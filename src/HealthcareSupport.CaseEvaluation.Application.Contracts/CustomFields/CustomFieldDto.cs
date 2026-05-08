using HealthcareSupport.CaseEvaluation.Enums;
using Volo.Abp.Application.Dtos;

namespace HealthcareSupport.CaseEvaluation.CustomFields;

/// <summary>
/// Read model for an IT-Admin-defined custom intake field. Mirrors OLD
/// <c>spm.CustomFields</c> row. Phase 6 (2026-05-03).
/// </summary>
public class CustomFieldDto : FullAuditedEntityDto<Guid>
{
    public Guid? TenantId { get; set; }
    public string FieldLabel { get; set; } = null!;
    public int DisplayOrder { get; set; }
    public CustomFieldType FieldType { get; set; }
    public int? FieldLength { get; set; }
    public string? MultipleValues { get; set; }
    public string? DefaultValue { get; set; }
    public bool IsMandatory { get; set; }
    public Guid? AppointmentTypeId { get; set; }
    public bool IsActive { get; set; }
}
