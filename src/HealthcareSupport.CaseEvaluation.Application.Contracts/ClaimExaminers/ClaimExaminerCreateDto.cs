using System;
using System.ComponentModel.DataAnnotations;

namespace HealthcareSupport.CaseEvaluation.ClaimExaminers;

public class ClaimExaminerCreateDto
{
    [StringLength(ClaimExaminerConsts.FirstNameMaxLength)]
    public string? FirstName { get; set; }

    [StringLength(ClaimExaminerConsts.LastNameMaxLength)]
    public string? LastName { get; set; }

    [EmailAddress]
    [StringLength(ClaimExaminerConsts.EmailMaxLength)]
    public string? Email { get; set; }

    [StringLength(ClaimExaminerConsts.PhoneNumberMaxLength)]
    public string? PhoneNumber { get; set; }

    [StringLength(ClaimExaminerConsts.FaxNumberMaxLength)]
    public string? FaxNumber { get; set; }

    [StringLength(ClaimExaminerConsts.StreetMaxLength)]
    public string? Street { get; set; }

    [StringLength(ClaimExaminerConsts.CityMaxLength)]
    public string? City { get; set; }

    [StringLength(ClaimExaminerConsts.ZipCodeMaxLength)]
    public string? ZipCode { get; set; }

    public Guid? StateId { get; set; }

    // IP6/UM4 record-based: optional -- a CE master may be created with no login;
    // the identity links later on self-register by email.
    public Guid? IdentityUserId { get; set; }
}
