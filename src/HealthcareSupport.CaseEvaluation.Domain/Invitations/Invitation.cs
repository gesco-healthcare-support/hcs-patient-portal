using System;
using HealthcareSupport.CaseEvaluation.ExternalSignups;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.Invitations;

/// <summary>
/// One-time-use, time-limited invitation for an external user to register
/// on a specific tenant portal. Issued by an internal staff user
/// (IT Admin / Staff Supervisor / Clinic Staff) via the admin
/// "User Management > Invite External User" page; consumed when the
/// recipient clicks the invite link and completes registration.
///
/// <para>State machine: <c>Active</c> (AcceptedAt == null AND ExpiresAt &gt;
/// now) -&gt; <c>Accepted</c> (AcceptedAt set, AcceptedByUserId set) or
/// <c>Expired</c> (AcceptedAt == null AND ExpiresAt &lt;= now). Once
/// accepted, the row is never reactivated -- the recipient must request
/// a fresh invite if they need to register again. Soft-delete (ISoftDelete
/// from FullAuditedAggregateRoot) is used for admin-side "revoke" in a
/// future ticket.</para>
///
/// <para>Token storage: the raw token is NEVER persisted. We hash it once
/// on issue (SHA256 -&gt; hex) and store only the hash in
/// <see cref="TokenHash"/>. A DB breach therefore does not leak active
/// invite URLs because reversing the hash is computationally infeasible
/// for a 256-bit input. The raw token is returned only once -- to the
/// AppService that built the URL.</para>
/// </summary>
public class Invitation : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public virtual Guid? TenantId { get; set; }

    /// <summary>
    /// Email address the invite was issued for. Locked at the register
    /// page so the recipient cannot register with a different address;
    /// the AppService re-validates against this column at accept time
    /// regardless of what the form posted.
    /// </summary>
    public virtual string Email { get; protected set; } = null!;

    /// <summary>
    /// External role the invite grants. Constrained to the four external
    /// roles by the calling AppService; the column itself accepts any
    /// enum value (defensive layering -- server-side validation re-runs
    /// at issue + accept time).
    /// </summary>
    public virtual ExternalUserType UserType { get; protected set; }

    /// <summary>
    /// Hex-encoded SHA256 hash of the raw invite token. The raw token is
    /// never stored. Unique-indexed so two simultaneous issuances cannot
    /// collide. Length matches <see cref="InvitationConsts.TokenHashLength"/>.
    /// </summary>
    public virtual string TokenHash { get; protected set; } = null!;

    /// <summary>
    /// UTC timestamp after which the invitation is no longer
    /// <c>Active</c>. Default 7 days from issue per
    /// <see cref="InvitationConsts.DefaultTtlDays"/>.
    /// </summary>
    public virtual DateTime ExpiresAt { get; protected set; }

    /// <summary>
    /// UTC timestamp of acceptance, set by
    /// <see cref="InvitationManager.AcceptAsync"/> when the recipient
    /// completes registration. Null while the invite is still
    /// <c>Active</c> or has <c>Expired</c> without being used.
    /// </summary>
    public virtual DateTime? AcceptedAt { get; protected set; }

    /// <summary>
    /// IdentityUser id created by the accepting registration. Null until
    /// acceptance succeeds. No DB FK constraint -- the IdentityUser row
    /// is owned by ABP Identity and we keep the cross-context reference
    /// loose for the same reason every other Invitation-touching code
    /// path does (the booking flow's PatientEmail / ApplicantAttorneyEmail
    /// columns follow the same convention).
    /// </summary>
    public virtual Guid? AcceptedByUserId { get; protected set; }

    /// <summary>
    /// IdentityUser id of the internal staff member who issued the
    /// invite. Required at issue time. No DB FK; treated as audit data.
    /// </summary>
    public virtual Guid InvitedByUserId { get; protected set; }

    protected Invitation()
    {
    }

    internal Invitation(
        Guid id,
        Guid? tenantId,
        string email,
        ExternalUserType userType,
        string tokenHash,
        DateTime expiresAt,
        Guid invitedByUserId)
    {
        Id = id;
        TenantId = tenantId;
        Email = email;
        UserType = userType;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
        InvitedByUserId = invitedByUserId;
    }

    /// <summary>
    /// Domain-service-only mutation: marks the invitation as accepted.
    /// Wrapped in the manager's atomic save so a race between two
    /// simultaneous accept attempts is resolved by the
    /// <c>ConcurrencyStamp</c> on the underlying aggregate root --
    /// the second writer fails with <c>DbUpdateConcurrencyException</c>
    /// and the AppService translates that to "already accepted".
    /// </summary>
    internal void MarkAccepted(Guid acceptedByUserId, DateTime acceptedAtUtc)
    {
        AcceptedAt = acceptedAtUtc;
        AcceptedByUserId = acceptedByUserId;
    }

    /// <summary>
    /// True when the invitation is still usable: not accepted, not
    /// expired, not soft-deleted. The AppService re-checks this on the
    /// validate endpoint and within the accept transaction, so callers
    /// must not rely on a stale value across awaits.
    /// </summary>
    public bool IsActive(DateTime nowUtc)
    {
        if (AcceptedAt.HasValue) return false;
        if (IsDeleted) return false;
        if (ExpiresAt <= nowUtc) return false;
        return true;
    }
}
