using System;

namespace HealthcareSupport.CaseEvaluation.ExternalSignups;

/// <summary>
/// Returned by the public <c>POST /api/public/external-signup/validate-invite</c>
/// endpoint when a token is in the <c>Active</c> state. The JS overlay
/// on <c>/Account/Register</c> consumes this response to prefill email
/// + role, mark both fields readonly, and render the
/// "You've been invited as &lt;Role&gt; by &lt;Tenant&gt;" banner.
///
/// <para>2026-05-15 -- intentionally a thin shape: no PII beyond the
/// email the invite was issued for, no internal user names or audit
/// data. The endpoint is anonymous, so the response surface stays
/// minimal.</para>
/// </summary>
public class InvitationValidationDto
{
    public string Email { get; set; } = null!;
    public ExternalUserType UserType { get; set; }
    public string RoleName { get; set; } = null!;
    public string TenantName { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
}
