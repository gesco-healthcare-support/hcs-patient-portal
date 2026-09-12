using System;
using System.ComponentModel.DataAnnotations;
using HealthcareSupport.CaseEvaluation.Validation;

namespace HealthcareSupport.CaseEvaluation.AppointmentPrimaryInsurances;

public class AppointmentPrimaryInsuranceCreateDto
{
    public Guid AppointmentId { get; set; }

    [StringLength(AppointmentPrimaryInsuranceConsts.NameMaxLength)]
    public string? Name { get; set; }

    [StringLength(AppointmentPrimaryInsuranceConsts.SuiteMaxLength)]
    public string? Suite { get; set; }

    [StringLength(AppointmentPrimaryInsuranceConsts.PhoneNumberMaxLength)]
    [PhoneNumber]
    public string? PhoneNumber { get; set; }

    [StringLength(AppointmentPrimaryInsuranceConsts.FaxNumberMaxLength)]
    [PhoneNumber]
    public string? FaxNumber { get; set; }

    [StringLength(AppointmentPrimaryInsuranceConsts.StreetMaxLength)]
    public string? Street { get; set; }

    [StringLength(AppointmentPrimaryInsuranceConsts.CityMaxLength)]
    public string? City { get; set; }

    [StringLength(AppointmentPrimaryInsuranceConsts.ZipMaxLength)]
    public string? Zip { get; set; }

    public Guid? StateId { get; set; }

    public bool IsActive { get; set; }
}
