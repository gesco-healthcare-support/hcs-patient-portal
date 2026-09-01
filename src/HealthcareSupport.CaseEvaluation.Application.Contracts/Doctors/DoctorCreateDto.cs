using HealthcareSupport.CaseEvaluation.Enums;
using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace HealthcareSupport.CaseEvaluation.Doctors;

public class DoctorCreateDto
{
    [Required]
    [StringLength(DoctorConsts.FirstNameMaxLength)]
    public string FirstName { get; set; } = null!;
    [Required]
    [StringLength(DoctorConsts.LastNameMaxLength)]
    public string LastName { get; set; } = null!;
    [Required]
    [EmailAddress]
    [StringLength(DoctorConsts.EmailMaxLength)]
    public string Email { get; set; } = null!;
    public Gender Gender { get; set; } = Enum.GetValues<Gender>()[0];

    public List<Guid> AppointmentTypeIds { get; set; } = new();

    public List<Guid> LocationIds { get; set; } = new();
}