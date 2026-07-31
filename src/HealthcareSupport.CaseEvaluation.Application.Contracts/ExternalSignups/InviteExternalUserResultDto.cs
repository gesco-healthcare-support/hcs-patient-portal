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
    public string InviteUrl { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string RoleName { get; set; } = null!;
    public string TenantName { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
}
