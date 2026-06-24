using System.ComponentModel.DataAnnotations;
using HealthcareSupport.CaseEvaluation.Enums;

namespace HealthcareSupport.CaseEvaluation.CustomFields;

/// <summary>
/// Input for creating an IT-Admin-defined custom intake field.
/// <c>DisplayOrder</c> is NOT accepted on input -- the AppService
/// auto-assigns <c>max(active.DisplayOrder) + 1</c> per OLD
/// <c>CustomFieldDomain.cs:55-61</c>. Mirrors OLD's add-form payload.
/// </summary>
public class CustomFieldCreateDto
{
    [Required]
    [StringLength(CustomFieldConsts.FieldLabelMaxLength)]
    public string FieldLabel { get; set; } = null!;

    [Required]
    public CustomFieldType FieldType { get; set; }

    public int? FieldLength { get; set; }

    [StringLength(CustomFieldConsts.MultipleValuesMaxLength)]
    public string? MultipleValues { get; set; }

    [StringLength(CustomFieldConsts.DefaultValueMaxLength)]
    public string? DefaultValue { get; set; }

    public bool IsMandatory { get; set; }

    /// <summary>
    /// Required by AppService rule -- a custom field always belongs to
    /// one AppointmentType. Schema permits null but the AppService
    /// rejects null per OLD UI contract.
    /// </summary>
    [Required]
    public Guid? AppointmentTypeId { get; set; }

    public bool IsActive { get; set; } = true;
}
