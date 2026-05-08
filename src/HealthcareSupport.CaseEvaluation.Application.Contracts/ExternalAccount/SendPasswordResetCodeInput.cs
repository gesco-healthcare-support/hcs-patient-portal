using System.ComponentModel.DataAnnotations;
using JetBrains.Annotations;

namespace HealthcareSupport.CaseEvaluation.ExternalAccount;

/// <summary>
/// Input DTO for
/// <see cref="IExternalAccountAppService.SendPasswordResetCodeAsync"/>.
/// Mirrors OLD <c>UserAuthenticationDomain.PostForgotPassword</c>'s
/// request shape (just an email field; OLD's <c>UserCredentialViewModel</c>
/// also carried <c>Password</c> + <c>FailedCount</c> but the forgot-password
/// path only uses <c>EmailId</c>).
/// </summary>
public class SendPasswordResetCodeInput
{
    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = null!;

    /// <summary>
    /// Optional return URL the AuthServer will redirect to after the user
    /// clicks the reset link. Round-tripped through the Razor reset page.
    /// Mirrors ABP's <c>SendPasswordResetCodeDto.ReturnUrl</c>.
    /// </summary>
    [CanBeNull]
    [StringLength(2048)]
    public string? ReturnUrl { get; set; }
}
