using System;
using HealthcareSupport.CaseEvaluation.Invitations;
using Volo.Abp.Application.Dtos;

namespace HealthcareSupport.CaseEvaluation.ExternalSignups;

/// <summary>
/// One row of the internal "Pending Invites" management list. Projects the
/// <see cref="Invitation"/> aggregate plus its derived
/// <see cref="InvitationStatus"/> and the resolved inviter display name. The
/// raw token is never exposed (it is not stored); a fresh URL is produced only
/// by resend.
/// </summary>
public class InvitationDto : EntityDto<Guid>
{
    public string Email { get; set; } = null!;
    public ExternalUserType UserType { get; set; }
    public string RoleName { get; set; } = null!;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }

    /// <summary>Optional firm name (attorney invites only).</summary>
    public string? FirmName { get; set; }

    /// <summary>IdentityUser id of the staff member who issued the invite.</summary>
    public Guid InvitedByUserId { get; set; }

    /// <summary>
    /// Resolved display name of the inviter (full name, else username/email).
    /// Null when the inviter cannot be resolved in the caller's tenant scope
    /// (e.g. a host IT Admin who issued the invite while impersonating).
    /// </summary>
    public string? InvitedByName { get; set; }

    /// <summary>When the invitation was first issued (UTC).</summary>
    public DateTime CreationTime { get; set; }

    /// <summary>When the current invite link expires (UTC); reset by resend.</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>When the recipient accepted, or null if not yet accepted.</summary>
    public DateTime? AcceptedAt { get; set; }

    public InvitationStatus Status { get; set; }
}
