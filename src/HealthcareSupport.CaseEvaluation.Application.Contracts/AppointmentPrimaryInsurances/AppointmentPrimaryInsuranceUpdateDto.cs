using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Domain.Entities;

namespace HealthcareSupport.CaseEvaluation.AppointmentPrimaryInsurances;

public class AppointmentPrimaryInsuranceUpdateDto : IHasConcurrencyStamp
{
    public Guid AppointmentInjuryDetailId { get; set; }

    [StringLength(AppointmentPrimaryInsuranceConsts.NameMaxLength)]
    public string? Name { get; set; }

    [StringLength(AppointmentPrimaryInsuranceConsts.InsuranceNumberMaxLength)]
    public string? InsuranceNumber { get; set; }

    [StringLength(AppointmentPrimaryInsuranceConsts.AttentionMaxLength)]
    public string? Attention { get; set; }

    [StringLength(AppointmentPrimaryInsuranceConsts.PhoneNumberMaxLength)]
    public string? PhoneNumber { get; set; }

    [StringLength(AppointmentPrimaryInsuranceConsts.FaxNumberMaxLength)]
    public string? FaxNumber { get; set; }

    [StringLength(AppointmentPrimaryInsuranceConsts.StreetMaxLength)]
    public string? Street { get; set; }

    [StringLength(AppointmentPrimaryInsuranceConsts.CityMaxLength)]
    public string? City { get; set; }

    [StringLength(AppointmentPrimaryInsuranceConsts.ZipMaxLength)]
    public string? Zip { get; set; }

    public Guid? StateId { get; set; }

    public bool IsActive { get; set; }

    public string ConcurrencyStamp { get; set; } = null!;
}
