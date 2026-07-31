using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace HealthcareSupport.CaseEvaluation.DefenseAttorneys;

public class DefenseAttorneyCreateDto
{
    // BUG-042 / UM4 (2026-06-05): First/Last name are first-class persisted fields.
    [StringLength(DefenseAttorneyConsts.FirstNameMaxLength)]
    public string? FirstName { get; set; }

    [StringLength(DefenseAttorneyConsts.LastNameMaxLength)]
    public string? LastName { get; set; }

    [StringLength(DefenseAttorneyConsts.FirmNameMaxLength)]
    public string? FirmName { get; set; }

    [StringLength(DefenseAttorneyConsts.FirmAddressMaxLength)]
    public string? FirmAddress { get; set; }

    [StringLength(DefenseAttorneyConsts.WebAddressMaxLength)]
    public string? WebAddress { get; set; }

    [StringLength(DefenseAttorneyConsts.EmailMaxLength)]
    public string? Email { get; set; }

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

    // UM4 (2026-06-05): optional -- record-based; identity linked later by email.
    public Guid? IdentityUserId { get; set; }
}
