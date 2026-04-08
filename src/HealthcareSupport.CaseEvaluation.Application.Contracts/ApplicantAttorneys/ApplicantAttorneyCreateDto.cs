using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace HealthcareSupport.CaseEvaluation.ApplicantAttorneys;

public class ApplicantAttorneyCreateDto
{
    [StringLength(ApplicantAttorneyConsts.FirmNameMaxLength)]
    public string? FirmName { get; set; }

    [StringLength(ApplicantAttorneyConsts.FirmAddressMaxLength)]
    public string? FirmAddress { get; set; }

    [StringLength(ApplicantAttorneyConsts.WebAddressMaxLength)]
    public string? WebAddress { get; set; }

    [StringLength(ApplicantAttorneyConsts.PhoneNumberMaxLength)]
    public string? PhoneNumber { get; set; }

    [StringLength(ApplicantAttorneyConsts.FaxNumberMaxLength)]
    public string? FaxNumber { get; set; }

    [StringLength(ApplicantAttorneyConsts.StreetMaxLength)]
    public string? Street { get; set; }

    [StringLength(ApplicantAttorneyConsts.CityMaxLength)]
    public string? City { get; set; }

    [StringLength(ApplicantAttorneyConsts.ZipCodeMaxLength)]
    public string? ZipCode { get; set; }

    public Guid? StateId { get; set; }

    public Guid IdentityUserId { get; set; }
}