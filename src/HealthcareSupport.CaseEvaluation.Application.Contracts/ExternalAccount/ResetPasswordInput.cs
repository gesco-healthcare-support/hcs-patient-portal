using System;
using System.ComponentModel.DataAnnotations;

namespace HealthcareSupport.CaseEvaluation.ExternalAccount;

/// <summary>
/// Input DTO for <see cref="IExternalAccountAppService.ResetPasswordAsync"/>.
/// Mirrors OLD <c>UserAuthenticationDomain.PutCredential</c>'s request
/// shape but with two changes:
/// <list type="bullet">
///   <item><c>UserId</c> is a <see cref="Guid"/> (ABP IdentityUser key)
///         instead of OLD's int.</item>
///   <item><c>ResetToken</c> is the cryptographic token from
///         <c>IdentityUserManager.GeneratePasswordResetTokenAsync</c>
///         (replaces OLD's stored <c>VerificationCode</c> GUID).</item>
/// </list>
/// </summary>
public class ResetPasswordInput
{
    [Required]
    public Guid UserId { get; set; }

    [Required]
    [StringLength(2048)]
    public string ResetToken { get; set; } = null!;

    [Required]
    [StringLength(128, MinimumLength = 6)]
    public string Password { get; set; } = null!;

    /// <summary>
    /// Mirrors OLD <c>PutCredentialValidation</c>'s
    /// <c>Password.Equals(ConfirmPassword)</c> check
    /// (<c>UserAuthenticationDomain.cs:222</c>).
    /// </summary>
    [Required]
    [StringLength(128, MinimumLength = 6)]
    public string ConfirmPassword { get; set; } = null!;
}
