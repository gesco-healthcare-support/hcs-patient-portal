using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace HealthcareSupport.CaseEvaluation.DefenseAttorneys;

public class DefenseAttorneyCreateDto
{
    [StringLength(DefenseAttorneyConsts.FirmNameMaxLength)]
    public string? FirmName { get; set; }

    [StringLength(DefenseAttorneyConsts.FirmAddressMaxLength)]
    public string? FirmAddress { get; set; }

    [StringLength(DefenseAttorneyConsts.WebAddressMaxLength)]
    public string? WebAddress { get; set; }

    [StringLength(DefenseAttorneyConsts.PhoneNumberMaxLength)]
    public string? PhoneNumber { get; set; }

    [StringLength(DefenseAttorneyConsts.FaxNumberMaxLength)]
    public string? FaxNumber { get; set; }

    [StringLength(DefenseAttorneyConsts.StreetMaxLength)]
    public string? Street { get; set; }

    [StringLength(DefenseAttorneyConsts.CityMaxLength)]
    public string? City { get; set; }

    [StringLength(DefenseAttorneyConsts.ZipCodeMaxLength)]
    public string? ZipCode { get; set; }

    public Guid? StateId { get; set; }

    public Guid IdentityUserId { get; set; }
}
