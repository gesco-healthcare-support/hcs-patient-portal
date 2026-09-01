using System.ComponentModel.DataAnnotations;

namespace HealthcareSupport.CaseEvaluation.ExternalAccount;

/// <summary>
/// Phase 1.D (Category 1, 2026-05-08) input DTO for
/// <see cref="IExternalAccountAppService.ResendEmailVerificationAsync"/>.
/// Carries only the email address; the user clicks "Send verification
/// email" on the post-register page, on the blocked-login error, or on
/// any other surface where they need a fresh confirmation link.
/// Returns generic success regardless of user existence to avoid
/// account-enumeration leak.
/// </summary>
public class ResendEmailVerificationInput
{
    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = null!;
}
