using HealthcareSupport.CaseEvaluation.Enums;
using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities;

namespace HealthcareSupport.CaseEvaluation.Doctors;

public class DoctorUpdateDto : IHasConcurrencyStamp
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
    public Gender Gender { get; set; }

    public List<Guid> AppointmentTypeIds { get; set; } = new();

    public List<Guid> LocationIds { get; set; } = new();

    public string ConcurrencyStamp { get; set; } = null!;
}