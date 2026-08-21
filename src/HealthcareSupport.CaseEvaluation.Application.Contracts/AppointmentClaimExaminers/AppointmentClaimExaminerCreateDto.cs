using System;
using System.ComponentModel.DataAnnotations;

namespace HealthcareSupport.CaseEvaluation.AppointmentClaimExaminers;

public class AppointmentClaimExaminerCreateDto
{
    public Guid AppointmentId { get; set; }

    // CI1 (2026-06-05): CE is required on every booking, so Name + Email are
    // mandatory when a CE record is created. Insurance stays optional.
    [Required]
    [StringLength(AppointmentClaimExaminerConsts.NameMaxLength)]
    public string? Name { get; set; }

    [StringLength(AppointmentClaimExaminerConsts.SuiteMaxLength)]
    public string? Suite { get; set; }

    [Required]
    [StringLength(AppointmentClaimExaminerConsts.EmailMaxLength)]
    public string? Email { get; set; }

    [StringLength(AppointmentClaimExaminerConsts.PhoneNumberMaxLength)]
    public string? PhoneNumber { get; set; }

    [StringLength(AppointmentClaimExaminerConsts.FaxMaxLength)]
    public string? Fax { get; set; }

    [StringLength(AppointmentClaimExaminerConsts.StreetMaxLength)]
    public string? Street { get; set; }

    [StringLength(AppointmentClaimExaminerConsts.CityMaxLength)]
    public string? City { get; set; }

    [StringLength(AppointmentClaimExaminerConsts.ZipMaxLength)]
    public string? Zip { get; set; }

    public Guid? StateId { get; set; }

    public bool IsActive { get; set; }
}
