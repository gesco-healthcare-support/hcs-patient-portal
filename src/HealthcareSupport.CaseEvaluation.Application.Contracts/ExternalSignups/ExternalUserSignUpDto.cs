using System;
using System.ComponentModel.DataAnnotations;
using JetBrains.Annotations;

namespace HealthcareSupport.CaseEvaluation.ExternalSignups;

/// <summary>
/// Public registration input. Carries the user's account credentials
/// (<c>Email</c>, <c>Password</c>, <c>ConfirmPassword</c>), the chosen
/// <c>UserType</c>, optional <c>FirstName</c> / <c>LastName</c>, the
/// attorney-only <c>FirmName</c> / <c>FirmEmail</c>, and the resolved
/// <c>TenantId</c>.
///
/// <para>OLD-parity fields per Phase 8 (2026-05-03):</para>
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
///
/// <para><c>FirmName</c> + <c>FirmEmail</c> persist to the IdentityUser
/// extension properties registered in Phase 2.4
/// (<c>CaseEvaluationModuleExtensionConfigurator</c>);
/// <c>FirstName</c> / <c>LastName</c> map to the stock IdentityUser
/// <c>Name</c> / <c>Surname</c> columns.</para>
/// </summary>
public class ExternalUserSignUpDto
{
    [Required(ErrorMessage = "Select your role.")]
    public ExternalUserType UserType { get; set; }

    // B17 (2026-05-07): OLD parity (PatientAppointment.DbEntities/Models/User.cs:64-85)
    // collects FirstName + LastName for every external role (Patient,
    // Adjuster/Claim Examiner, Patient/Applicant Attorney, Defense
    // Attorney) and persists them as separate columns. The Razor
    // register form now shows both fields for all roles -- only FirmName
    // is attorney-only. These remain nullable here so the booking-form
    // backfill path stays available, but the typical register submission
    // sends both populated.
    [StringLength(128)]
    public string? FirstName { get; set; }

    [StringLength(128)]
    public string? LastName { get; set; }

    [Required(ErrorMessage = "Enter your email.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(256)]
    public string Email { get; set; } = null!;

    [Required(ErrorMessage = "Enter a password.")]
    [StringLength(128, MinimumLength = 6)]
    public string Password { get; set; } = null!;

    /// <summary>
    /// Phase 8 (2026-05-03) -- mirrors OLD's <c>UserDomain.cs:88</c>
    /// <c>UserPassword == ConfirmPassword</c> check for external users.
    /// AppService rejects the registration with
    /// <c>RegistrationConfirmPasswordMismatch</c> on mismatch.
    /// </summary>
    [Required(ErrorMessage = "Confirm your password.")]
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

    /// <summary>
    /// 2026-05-15 -- optional one-time invitation token. When supplied,
    /// the AppService re-validates the token against the persisted
    /// <c>Invitation</c> row, ignores the form's <see cref="Email"/> +
    /// <see cref="UserType"/> (uses the server-resolved values from the
    /// invitation), and atomically marks the invitation accepted in
    /// the same transaction as the user create. When absent, the
    /// AppService falls through to the anonymous self-register path
    /// (existing behavior preserved).
    ///
    /// <para>The token is Base64-URL encoded (~43 chars). Length cap of
    /// 64 defends against random URL fuzzing without a DB roundtrip.</para>
    /// </summary>
    [CanBeNull]
    [StringLength(64)]
    public string? InviteToken { get; set; }
}
