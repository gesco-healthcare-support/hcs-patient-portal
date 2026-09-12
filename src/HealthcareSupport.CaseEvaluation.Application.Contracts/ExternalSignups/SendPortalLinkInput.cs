using System;
using System.ComponentModel.DataAnnotations;

namespace HealthcareSupport.CaseEvaluation.ExternalSignups;

/// <summary>
/// Item F (2026-08-22) -- request to email an existing external user a link to their office portal.
///
/// <para>Carries no role: the recipient already has an account, and this only sends them the
/// sign-in page. Changing someone's role is a separate administrative action.</para>
/// </summary>
public class SendPortalLinkInput
{
    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = null!;

    /// <summary>
    /// The target office. Mirrors <see cref="InviteExternalUserDto.TenantId"/>: a host-scope caller
    /// picks the office explicitly, an in-office caller leaves it null and keeps their ambient
    /// tenant.
    /// </summary>
    public Guid? TenantId { get; set; }
}
