using System;
using System.ComponentModel.DataAnnotations;
using JetBrains.Annotations;

namespace HealthcareSupport.CaseEvaluation.ExternalSignups;

/// <summary>
/// Public registration input. Phase 8 (2026-05-03) extends the original
/// minimal DTO with three OLD-parity fields:
/// <list type="bullet">
///   <item><c>ConfirmPassword</c> -- OLD <c>UserDomain.cs:88</c> requires
///         match for ExternalUser; NEW validates equally.</item>
///   <item><c>FirmName</c> -- OLD <c>UserDomain.cs:104-108</c> persists for
///         Applicant + Defense Attorney roles; required (validated in
///         AppService).</item>
///   <item><c>FirmEmail</c> -- OLD <c>UserDomain.cs:106</c> auto-derives from
///         <c>EmailId.ToLower()</c>; NEW accepts an explicit value or
///         auto-derives when not provided.</item>
/// </list>
/// All three persist to the IdentityUser extension props registered in
/// Phase 2.4 (<c>CaseEvaluationModuleExtensionConfigurator</c>).
/// </summary>
public class ExternalUserSignUpDto
{
    [Required]
    public ExternalUserType UserType { get; set; }

    // Adrian (2026-04-30): names are NOT collected on the register page.
    // They are captured later on the booking form's patient/AA section, so
    // these are nullable here. The server stores them as-is on IdentityUser
    // (Name/Surname) when supplied; otherwise leaves them null.
    [StringLength(128)]
    public string? FirstName { get; set; }

    [StringLength(128)]
    public string? LastName { get; set; }

    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = null!;

    [Required]
    [StringLength(128, MinimumLength = 6)]
    public string Password { get; set; } = null!;

    /// <summary>
    /// Phase 8 (2026-05-03) -- mirrors OLD's <c>UserDomain.cs:88</c>
    /// <c>UserPassword == ConfirmPassword</c> check for external users.
    /// AppService rejects the registration with
    /// <c>RegistrationConfirmPasswordMismatch</c> on mismatch.
    /// </summary>
    [Required]
    [StringLength(128, MinimumLength = 6)]
    public string ConfirmPassword { get; set; } = null!;

    /// <summary>
    /// Required for Applicant Attorney + Defense Attorney roles
    /// (validated in AppService). Persisted to the
    /// <c>FirmName</c> IdentityUser extension property registered in
    /// Phase 2.4.
    /// </summary>
    [CanBeNull]
    [StringLength(256)]
    public string? FirmName { get; set; }

    /// <summary>
    /// Optional. When omitted for an attorney role, the AppService auto-
    /// derives this from <c>Email.ToLower()</c> verbatim with OLD
    /// (<c>UserDomain.cs:106</c>). Persisted to the <c>FirmEmail</c>
    /// IdentityUser extension property.
    /// </summary>
    [CanBeNull]
    [EmailAddress]
    [StringLength(256)]
    public string? FirmEmail { get; set; }

    public Guid? TenantId { get; set; }
}
