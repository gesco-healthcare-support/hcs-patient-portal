using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Domain.Entities;

namespace HealthcareSupport.CaseEvaluation.AppointmentClaimExaminers;

public class AppointmentClaimExaminerUpdateDto : IHasConcurrencyStamp
{
    public Guid AppointmentInjuryDetailId { get; set; }

    [StringLength(AppointmentClaimExaminerConsts.NameMaxLength)]
    public string? Name { get; set; }

    [StringLength(AppointmentClaimExaminerConsts.ClaimExaminerNumberMaxLength)]
    public string? ClaimExaminerNumber { get; set; }

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

    public string ConcurrencyStamp { get; set; } = null!;
}
