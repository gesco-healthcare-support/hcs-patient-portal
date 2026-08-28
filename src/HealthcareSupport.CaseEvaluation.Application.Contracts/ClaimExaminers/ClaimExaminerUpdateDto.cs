using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Domain.Entities;
using HealthcareSupport.CaseEvaluation.Validation;

namespace HealthcareSupport.CaseEvaluation.ClaimExaminers;

public class ClaimExaminerUpdateDto : IHasConcurrencyStamp
{
    [StringLength(ClaimExaminerConsts.FirstNameMaxLength)]
    public string? FirstName { get; set; }

    [StringLength(ClaimExaminerConsts.LastNameMaxLength)]
    public string? LastName { get; set; }

    [EmailAddress]
    [StringLength(ClaimExaminerConsts.EmailMaxLength)]
    public string? Email { get; set; }

    [StringLength(ClaimExaminerConsts.PhoneNumberMaxLength)]
    [PhoneNumber]
    public string? PhoneNumber { get; set; }

    [StringLength(ClaimExaminerConsts.FaxNumberMaxLength)]
    [PhoneNumber]
    public string? FaxNumber { get; set; }

    [StringLength(ClaimExaminerConsts.StreetMaxLength)]
    public string? Street { get; set; }

    [StringLength(ClaimExaminerConsts.CityMaxLength)]
    public string? City { get; set; }

    [StringLength(ClaimExaminerConsts.ZipCodeMaxLength)]
    public string? ZipCode { get; set; }

    public Guid? StateId { get; set; }

    public Guid? IdentityUserId { get; set; }

    public string ConcurrencyStamp { get; set; } = null!;
}
