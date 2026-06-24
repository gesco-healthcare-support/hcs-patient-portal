using System;
using System.ComponentModel.DataAnnotations;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocumentTypes;

public class AppointmentDocumentTypeUpdateDto
{
    [Required]
    [StringLength(AppointmentDocumentTypeConsts.NameMaxLength)]
    public string Name { get; set; } = null!;

    /// <summary>Optional appointment-type scope. Null means the category applies
    /// to every appointment type.</summary>
    public Guid? AppointmentTypeId { get; set; }

    public bool IsActive { get; set; } = true;
}
