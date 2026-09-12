using System.ComponentModel.DataAnnotations;

namespace HealthcareSupport.CaseEvaluation.Doctors;

/// <summary>
/// Input for creating a COMPLETE practice via
/// <c>DoctorTenantAppService.CreatePracticeAsync</c>: a Volo SaaS tenant + a provisioned
/// office database + the owner doctor + host-side branding. Distinct from the stock SaaS
/// create (which writes only a bare tenant row). The slug is the office subdomain +
/// <c>CaseEvaluation_{slug}</c> database token, validated server-side by TenantNaming; the
/// display name defaults to "Dr. {FirstName} {LastName}" (PracticeNaming) when left blank.
/// String lengths mirror the domain (TenantNaming / DoctorConsts / OfficeBranding) as a
/// first-line 400 check; the authoritative validation stays server-side.
/// </summary>
public class CreatePracticeInput
{
    /// <summary>Office subdomain + CaseEvaluation_{slug} database token (DNS-safe, not "admin").</summary>
    [Required]
    [StringLength(63)] // TenantNaming.MaxSlugLength
    public string Slug { get; set; } = string.Empty;

    /// <summary>Owner doctor's first name.</summary>
    [Required]
    [StringLength(50)] // DoctorConsts.FirstNameMaxLength
    public string DoctorFirstName { get; set; } = string.Empty;

    /// <summary>Owner doctor's last name (the practice slug is generally this surname).</summary>
    [Required]
    [StringLength(50)] // DoctorConsts.LastNameMaxLength
    public string DoctorLastName { get; set; } = string.Empty;

    /// <summary>
    /// Owner doctor's email. Also serves as the office admin login (one doctor per
    /// practice, so the doctor owns and signs into the office); the admin sets a password
    /// via the forgot-password flow.
    /// </summary>
    [Required]
    [EmailAddress]
    [StringLength(49)] // DoctorConsts.EmailMaxLength (also the admin login)
    public string DoctorEmail { get; set; } = string.Empty;

    /// <summary>Optional branding display name; defaults to "Dr. {FirstName} {LastName}".</summary>
    [StringLength(128)] // OfficeBranding.DisplayNameMaxLength
    public string? DisplayName { get; set; }
}
