using System.ComponentModel.DataAnnotations;

namespace HealthcareSupport.CaseEvaluation.ExternalSignups;

/// <summary>
/// D.2 (2026-04-30): admin-side invite request for an external user. The
/// invite is link-only (no token, no expiry) -- the caller receives a
/// tenant-specific `/Account/Register?__tenant=&amp;email=&amp;role=` URL that the
/// recipient opens to self-register. Internal roles (admin / Staff Supervisor
/// / Clinic Staff / Doctor) are intentionally NOT invitable here -- they are
/// created by tenant admins via ABP's Identity > Users page so external users
/// can never see internal-role registration paths even if a malformed URL
/// were crafted.
/// </summary>
public class InviteExternalUserDto
{
    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = null!;

    /// <summary>
    /// Restricted to the four external roles: Patient, ApplicantAttorney,
    /// DefenseAttorney, ClaimExaminer. Validated server-side; any other
    /// value (including internal role names) returns 400.
    /// </summary>
    [Required]
    public ExternalUserType UserType { get; set; }
}

/// <summary>
/// D.2 (2026-04-30): response shape for the invite endpoint. Includes the
/// constructed register URL so the admin can copy + paste it manually until
/// real SMTP credentials land (every dev-stack invite returns this URL even
/// after the email is enqueued, so the admin has a fallback when the send
/// fails silently). Gated to Development environment via a banner in the UI.
/// </summary>
public class InviteExternalUserResultDto
{
    public string InviteUrl { get; set; } = null!;
    public bool EmailEnqueued { get; set; }
    public string Email { get; set; } = null!;
    public string RoleName { get; set; } = null!;
    public string TenantName { get; set; } = null!;
}
