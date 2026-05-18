using System.ComponentModel.DataAnnotations;

namespace HealthcareSupport.CaseEvaluation.ExternalSignups;

/// <summary>
/// Admin-side request to invite an external user to register on the
/// current tenant portal. Internal roles (admin, IT Admin, Staff
/// Supervisor, Clinic Staff) call this; the AppService gate is
/// permission-based (<c>CaseEvaluation.UserManagement.InviteExternalUser</c>).
///
/// <para>2026-05-15 -- the invite produces a one-time-use, 7-day-TTL
/// token. The recipient receives a URL of the form
/// <c>{authServerBaseUrl}/Account/Register?inviteToken=&lt;raw-token&gt;</c>.
/// The JS overlay on the register page validates the token, prefills
/// + locks email + role, and atomically marks the invitation accepted
/// when the recipient completes registration.</para>
///
/// <para>Internal roles (admin, IT Admin, Staff Supervisor, Clinic Staff,
/// Doctor) are intentionally NOT invitable here: the
/// <see cref="ExternalUserType"/> enum value is constrained to the four
/// external roles, and the AppService re-validates server-side. A
/// tampered URL cannot register as an internal role.</para>
/// </summary>
public class InviteExternalUserDto
{
    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = null!;

    /// <summary>
    /// External role the invitation grants. Restricted to the four
    /// external roles: Patient, ApplicantAttorney, DefenseAttorney,
    /// ClaimExaminer. The AppService rejects any other value with 400.
    /// </summary>
    [Required]
    public ExternalUserType UserType { get; set; }
}
