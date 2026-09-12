using System;
using Volo.Abp.Domain.Entities;

namespace HealthcareSupport.CaseEvaluation.MyClaimExaminerProfiles;

/// <summary>
/// R2-4 (2026-06-22): the signed-in claim examiner's own master profile, returned by
/// the self-scoped <see cref="IMyClaimExaminerProfileAppService"/>. Mirrors the
/// attorney self-profile (#9) but carries no firm fields -- a claim examiner's schema
/// differs by design. Email is read-only here (account identity, not self-editable).
/// </summary>
public class MyClaimExaminerProfileDto : IHasConcurrencyStamp
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? FaxNumber { get; set; }
    public string? Street { get; set; }
    public string? City { get; set; }
    public Guid? StateId { get; set; }
    public string? ZipCode { get; set; }
    public string? Email { get; set; }
    public string ConcurrencyStamp { get; set; } = null!;
}
