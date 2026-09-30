using System.ComponentModel.DataAnnotations;
using HealthcareSupport.CaseEvaluation.Enums;

namespace HealthcareSupport.CaseEvaluation.CustomFields;

/// <summary>
/// Update payload. <c>DisplayOrder</c> IS editable here so admins can
/// re-order existing fields explicitly (the auto-assign-on-create rule
/// only governs initial position).
/// </summary>
public class CustomFieldUpdateDto
{
    [Required]
    [StringLength(CustomFieldConsts.FieldLabelMaxLength)]
    public string FieldLabel { get; set; } = null!;

    public int DisplayOrder { get; set; }

    [Required]
    public CustomFieldType FieldType { get; set; }

    public int? FieldLength { get; set; }

    [StringLength(CustomFieldConsts.MultipleValuesMaxLength)]
    public string? MultipleValues { get; set; }

    [StringLength(CustomFieldConsts.DefaultValueMaxLength)]
    public string? DefaultValue { get; set; }

    public bool IsMandatory { get; set; }

    [Required]
    public Guid? AppointmentTypeId { get; set; }

    public bool IsActive { get; set; }
}
