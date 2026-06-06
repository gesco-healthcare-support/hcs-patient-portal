using System;

namespace HealthcareSupport.CaseEvaluation.ExternalSignups;

/// <summary>
/// Returned by the public <c>POST /api/public/external-signup/validate-invite</c>
/// endpoint when a token is in the <c>Active</c> state. The JS overlay
/// on <c>/Account/Register</c> consumes this response to prefill email
/// + role, mark both fields readonly, and render the
/// "You've been invited as &lt;Role&gt; by &lt;Tenant&gt;" banner.
///
/// <para>2026-06-04 (UM1) -- also carries the recipient's own First/Last
/// name (when the inviter supplied them) so the register page can pre-fill
/// the editable name fields. Still no internal-staff names or audit data;
/// the anonymous endpoint only ever returns the invitee's own details for
/// their own invite, so the surface stays minimal.</para>
/// </summary>
public class InvitationValidationDto
{
    public string Email { get; set; } = null!;
    public ExternalUserType UserType { get; set; }
    public string RoleName { get; set; } = null!;
    public string TenantName { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Optional recipient first name stored on the invitation. Surfaced so
    /// the register page can pre-fill the (editable) name field. Null when
    /// the inviter did not provide a name.
    /// </summary>
    public string? FirstName { get; set; }

    /// <summary>Optional recipient last name stored on the invitation.</summary>
    public string? LastName { get; set; }
}
