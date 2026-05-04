using System;

namespace HealthcareSupport.CaseEvaluation.Notifications.Events;

/// <summary>
/// Phase 18 (2026-05-04) -- raised after a successful external-user
/// registration in <c>ExternalSignupAppService.RegisterAsync</c>. Mirrors
/// OLD <c>EmailTemplate.UserRegistered</c> trigger.
///
/// <para>This is a NEW Eto rather than ABP's <c>UserCreatedEto</c>:
/// (a) ABP's event fires for every IdentityUser create including
/// internal-tier seed users, which we do NOT want to email; (b) we need
/// the verbatim role string + tenant context to render the verification
/// link with the right tenant slug. Phase 8 (Registration) emits this.
/// Phase 9 / Phase 10 keep their inline email send for the
/// resend-confirmation + post-reset confirmation flows since those
/// already have a stable inline path
/// (<c>ExternalAccountAppService.SendPasswordResetCodeAsync</c>).</para>
/// </summary>
public class ExternalUserRegisteredEto
{
    public Guid UserId { get; set; }

    public Guid? TenantId { get; set; }

    public string Email { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    /// <summary>Localized role name (Patient / Applicant Attorney / Defense Attorney / Adjuster).</summary>
    public string RoleName { get; set; } = string.Empty;

    /// <summary>Tenant display name -- needed to render branded subject + footer.</summary>
    public string? TenantName { get; set; }

    public DateTime OccurredAt { get; set; }
}
