using System;

namespace HealthcareSupport.CaseEvaluation.ExternalSignups;

/// <summary>
/// Read DTO for the current authenticated user's profile, returned by
/// <c>ExternalSignupAppService.GetMyProfileAsync</c>. The Angular SPA
/// reads this immediately after login to drive post-login routing
/// (external -> <c>/home</c>, internal -> <c>/dashboard</c>) and to surface
/// the accessor-onboarding nudge.
///
/// Phase 9 (2026-05-03) added <see cref="IsExternalUser"/> and
/// <see cref="IsAccessor"/> -- both backed by the Phase 2.4 IdentityUser
/// extension props registered in
/// <c>CaseEvaluationModuleExtensionConfigurator</c>. Without them, the SPA
/// has no way to distinguish external from internal accounts after the
/// OAuth2 token round-trip.
/// </summary>
public class ExternalUserProfileDto
{
    public Guid IdentityUserId { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string UserRole { get; set; } = string.Empty;

    /// <summary>
    /// True for the four external roles (Patient / Adjuster /
    /// Applicant Attorney / Defense Attorney) per Phase 2.4 extension
    /// property registration. Replaces OLD's <c>UserType.External=1</c>
    /// flag returned in the login response (<c>UserAuthenticationDomain.cs:99</c>).
    /// </summary>
    public bool IsExternalUser { get; set; }

    /// <summary>
    /// True when this user was provisioned via the appointment-share
    /// invite flow (per Phase 2.4 extension property <c>IsAccessor</c>).
    /// Mirrors OLD <c>UserAuthenticationDomain.cs:107</c>'s
    /// <c>userAuthentication.IsAccessor</c> field. The SPA shows a
    /// first-login onboarding prompt when true.
    /// </summary>
    public bool IsAccessor { get; set; }

    /// <summary>
    /// Phase 1 / C2 / D4 (firm-based AA/DA registration, 2026-06-11) -- the
    /// attorney's law-firm name, read from the <c>FirmName</c> IdentityUser
    /// extension property. Empty for non-attorney roles and for attorneys
    /// registered before firm capture. The SPA home banner falls back to this
    /// (via <c>resolveExternalUserDisplayName</c>) when Name/Surname are blank,
    /// which is the normal shape for a firm account (D1).
    /// </summary>
    public string FirmName { get; set; } = string.Empty;
}
