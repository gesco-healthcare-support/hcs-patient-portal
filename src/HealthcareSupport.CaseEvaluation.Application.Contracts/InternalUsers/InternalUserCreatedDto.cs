using System;

namespace HealthcareSupport.CaseEvaluation.InternalUsers;

/// <summary>
/// Response for <see cref="IInternalUsersAppService.CreateAsync"/>.
/// Carries the resolved user identity + the per-tenant display name +
/// a boolean flag for whether the welcome email was successfully
/// queued via Hangfire.
///
/// <para><b>Security:</b> the auto-generated temporary password is
/// NEVER returned in this DTO. The welcome email is the only channel
/// the password leaves the server through. If
/// <see cref="WelcomeEmailQueued"/> is false, the IT Admin must reset
/// the password through the ABP Identity admin UI rather than read it
/// from an API response.</para>
/// </summary>
public class InternalUserCreatedDto
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string RoleName { get; set; } = null!;
    public string TenantName { get; set; } = null!;

    /// <summary>
    /// True when the welcome email reached the Hangfire queue without
    /// throwing. False on dispatch failure (network blip, template
    /// missing, etc.); the user row + role assignment still committed,
    /// so the IT Admin should manually reset the password and re-send
    /// the welcome email rather than re-running the create endpoint.
    /// </summary>
    public bool WelcomeEmailQueued { get; set; }
}
