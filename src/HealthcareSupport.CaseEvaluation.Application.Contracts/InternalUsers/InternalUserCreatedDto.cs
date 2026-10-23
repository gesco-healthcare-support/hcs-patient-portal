using System;

namespace HealthcareSupport.CaseEvaluation.InternalUsers;

/// <summary>
/// Response for <see cref="IInternalUsersAppService.CreateAsync"/>.
/// Carries the resolved user identity + the per-tenant display name +
/// a boolean flag for whether the activation email was successfully
/// queued via Hangfire.
///
/// <para><b>Security:</b> no password exists for anyone to read. Since
/// 2026-10-23 the account is created with a password nobody sees and the
/// person chooses their own from the activation link in that email.</para>
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
    /// True when the activation email reached the Hangfire queue without
    /// throwing. False on dispatch failure (network blip, template
    /// missing, etc.); the user row + role assignment still committed,
    /// so the operator should press the reset button on the user's row
    /// (it sends a fresh activation link to an account not yet active)
    /// rather than re-running the create endpoint. The name predates the
    /// activation email and is kept because the generated client uses it.
    /// </summary>
    public bool WelcomeEmailQueued { get; set; }
}
