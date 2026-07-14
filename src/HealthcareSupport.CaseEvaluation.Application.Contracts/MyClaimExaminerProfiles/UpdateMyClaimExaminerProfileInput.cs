using System;

namespace HealthcareSupport.CaseEvaluation.MyClaimExaminerProfiles;

/// <summary>
/// R2-4 (2026-06-22): self-edit payload for the signed-in claim examiner. Carries NO
/// examiner id -- the service resolves the caller's own master from CurrentUser.Id, so
/// a caller can never address another party's record. Email is not included (identity
/// is not self-editable here).
/// </summary>
public class UpdateMyClaimExaminerProfileInput
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? FaxNumber { get; set; }
    public string? Street { get; set; }
    public string? City { get; set; }
    public Guid? StateId { get; set; }
    public string? ZipCode { get; set; }
    public string? ConcurrencyStamp { get; set; }
}
