using System;

namespace HealthcareSupport.CaseEvaluation.MyAttorneyProfiles;

/// <summary>
/// #9 (2026-06-19): self-edit payload for the signed-in attorney. Deliberately carries
/// NO attorney id -- the service resolves the caller's own master from CurrentUser.Id, so
/// a caller can never address another party's record. Email is not included (identity is
/// not self-editable here).
/// </summary>
public class UpdateMyAttorneyProfileInput
{
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
    public string? ConcurrencyStamp { get; set; }
}
