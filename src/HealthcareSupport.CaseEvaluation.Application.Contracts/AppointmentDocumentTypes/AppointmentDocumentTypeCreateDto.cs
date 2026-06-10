using System;
using System.ComponentModel.DataAnnotations;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocumentTypes;

public class AppointmentDocumentTypeCreateDto
{
    [Required]
    [StringLength(AppointmentDocumentTypeConsts.NameMaxLength)]
    public string Name { get; set; } = null!;

    /// <summary>Optional appointment-type scope. Null creates a category that
    /// applies to every appointment type.</summary>
    public Guid? AppointmentTypeId { get; set; }

    public bool IsActive { get; set; } = true;
}
