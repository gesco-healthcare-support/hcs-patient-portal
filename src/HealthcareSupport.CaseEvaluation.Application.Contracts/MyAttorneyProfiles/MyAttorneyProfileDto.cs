using System;
using Volo.Abp.Domain.Entities;

namespace HealthcareSupport.CaseEvaluation.MyAttorneyProfiles;

/// <summary>
/// #9 (2026-06-19): the signed-in attorney's own master profile. Returned by the
/// self-scoped <see cref="IMyAttorneyProfileAppService"/>. Email is read-only here
/// (it is the account identity, not self-editable on this surface).
/// </summary>
public class MyAttorneyProfileDto : IHasConcurrencyStamp
{
    /// <summary>"applicant" or "defense" -- which master backs this profile.</summary>
    public string Kind { get; set; } = null!;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? FirmName { get; set; }
    public string? WebAddress { get; set; }
    public string? PhoneNumber { get; set; }
    public string? FaxNumber { get; set; }
    public string? Street { get; set; }
    public string? City { get; set; }
    public Guid? StateId { get; set; }
    public string? ZipCode { get; set; }
    public string? Email { get; set; }
    public string ConcurrencyStamp { get; set; } = null!;
}
