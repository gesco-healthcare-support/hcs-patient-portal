using System;

namespace HealthcareSupport.CaseEvaluation.ExternalSignups;

/// <summary>
/// Response shape for the invite endpoint. Returns the constructed
/// invite URL so the admin can copy and share manually when SMTP
/// delivery is unreliable, plus the resolved role + tenant labels for
/// the on-screen confirmation. <see cref="ExpiresAt"/> tells the admin
/// when the link goes inactive so they can plan a re-issue if needed.
///
/// <para>2026-05-15 -- removed <c>EmailEnqueued</c> field (always true
/// when the AppService returns 200). The Hangfire job has its own
/// retry pipeline; surfacing the queue state to the UI was confusing
/// for staff. The "always copy the link manually" UX absorbs the
/// dispatch-failed case without needing a separate flag.</para>
/// </summary>
public class InviteExternalUserResultDto
{
    /// <summary>
    /// Null when <see cref="AlreadyRegistered"/> is true: no invitation was issued, so there is no
    /// link to copy.
    /// </summary>
    public string? InviteUrl { get; set; }

    public string Email { get; set; } = null!;
    public string RoleName { get; set; } = null!;
    public string TenantName { get; set; } = null!;

    /// <summary>Default when <see cref="AlreadyRegistered"/> is true -- nothing expires.</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Item F (2026-08-22) -- true when the email already has an account in this office, in which
    /// case NO invitation was issued.
    ///
    /// <para>This used to throw <c>InviteEmailAlreadyRegistered</c>. It is not an error: the person
    /// simply has an account already, and staff usually want to send them a sign-in link rather than
    /// be told off. Returning it as a result also lets the UI offer that action instead of rendering
    /// a red box that leaves the caller stuck on the phone.</para>
    /// </summary>
    public bool AlreadyRegistered { get; set; }

    /// <summary>
    /// The role the EXISTING account holds, which may differ from the role being invited. Populated
    /// only when <see cref="AlreadyRegistered"/> is true. Naming it is the useful part: it tells
    /// staff at a glance whether they have the wrong person or merely the wrong role.
    /// </summary>
    public string? ExistingRoleName { get; set; }
}
