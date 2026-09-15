using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocumentTypes;

public class AppointmentDocumentTypeCreateDto
{
    [Required]
    [StringLength(AppointmentDocumentTypeConsts.NameMaxLength)]
    public string Name { get; set; } = null!;

    /// <summary>The appointment types this category is offered for. Ignored when
    /// <see cref="AppliesToAll"/> is true.</summary>
    public List<Guid> AppointmentTypeIds { get; set; } = new();

    /// <summary>True to offer this category for every appointment type.</summary>
    public bool AppliesToAll { get; set; }

    public bool IsActive { get; set; } = true;
}
