using System;
using System.ComponentModel.DataAnnotations;

namespace HealthcareSupport.CaseEvaluation.InternalUsers;

/// <summary>
/// Request body for <see cref="IInternalUsersAppService.CreateAsync"/>. IT
/// Admin submits this from the SPA's <c>/internal-users</c> page; the
/// AppService re-validates every field server-side regardless of any
/// client-side checks.
///
/// <para><b>2026-05-15 design note:</b> the dropdown on the SPA form
/// only offers <c>Clinic Staff</c> and <c>Staff Supervisor</c> (the
/// two roles IT Admin is allowed to create per OLD parity), but the
/// server-side allow-list in <see cref="InternalUsersAppService"/>
/// rejects any other value -- a tampered request body cannot register
/// as another role.</para>
///
/// <para>External user roles (Patient / Applicant Attorney / Defense
/// Attorney / Claim Examiner) are intentionally NOT creatable through
/// this surface; those go through <c>InviteExternalUserAsync</c>
/// instead. IT Admin self-creation is also rejected (IT Admin accounts
/// are seeded only).</para>
/// </summary>
public class CreateInternalUserDto
{
    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = null!;

    [Required]
    [StringLength(64, MinimumLength = 1)]
    public string FirstName { get; set; } = null!;

    [Required]
    [StringLength(64, MinimumLength = 1)]
    public string LastName { get; set; } = null!;

    /// <summary>
    /// Role display name. Server-side allow-list:
    /// <c>{ "Clinic Staff", "Staff Supervisor" }</c>. Any other value
    /// returns <c>BusinessException(InternalUserInvalidRole)</c> ->
    /// HTTP 400.
    /// </summary>
    [Required]
    [StringLength(64, MinimumLength = 1)]
    public string RoleName { get; set; } = null!;

    /// <summary>
    /// Tenant the new user belongs to. IT Admin is host-scoped
    /// (admin.localhost), so <c>CurrentTenant.Id</c> is null at request
    /// time; the form's tenant picker is the only source of truth for
    /// which tenant the user lands in. <c>Guid.Empty</c> is rejected
    /// with <c>BusinessException(InternalUserTenantRequired)</c>.
    /// </summary>
    [Required]
    public Guid TenantId { get; set; }

    /// <summary>
    /// Optional phone number; persisted on the
    /// <c>IdentityUser.PhoneNumber</c> column with <c>confirmed: false</c>.
    /// </summary>
    [StringLength(20)]
    public string? PhoneNumber { get; set; }
}
