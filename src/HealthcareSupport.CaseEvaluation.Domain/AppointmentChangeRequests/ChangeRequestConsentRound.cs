using System;
using Volo.Abp;
using Volo.Abp.Auditing;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

/// <summary>
/// One staff-confirmed reschedule date and the consent both sides gave (or withheld) for it
/// (epic phase 4c, 2026-08-05). Rounds are rows rather than columns because the full audit
/// trail of every date proposed -- and who declined it -- has to stay QUERYABLE, not
/// reconstructed from a change log.
///
/// <para>A round also IS the reset mechanism. Consent is single-issue per side
/// (<see cref="IssueSideConsent"/> is only valid from
/// <see cref="ChangeRequestConsentStatus.NotRequired"/>), so a declined or expired date
/// cannot be re-solicited on the same row. Staff pick a different date, the current round is
/// superseded, and round N+1 starts fresh at <c>NotRequired</c> on both sides.</para>
///
/// <para>Consent for CANCELLATION requests is untouched by this type -- it stays on the
/// parent <see cref="AppointmentChangeRequest"/>'s flat columns, because a cancellation has
/// no proposed date to re-propose.</para>
/// </summary>
[Audited]
public class ChangeRequestConsentRound : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public virtual Guid? TenantId { get; set; }

    public virtual Guid AppointmentChangeRequestId { get; protected set; }

    /// <summary>1-based; unique per change request. Round N+1 supersedes round N.</summary>
    public virtual int RoundNumber { get; protected set; }

    /// <summary>The slot staff confirmed for this round. Both sides consent to THIS date.</summary>
    public virtual Guid ProposedDoctorAvailabilityId { get; protected set; }

    /// <summary>Staff user who confirmed the date (audit).</summary>
    public virtual Guid? ProposedByUserId { get; protected set; }

    /// <summary>
    /// Staff note explaining THIS date. Held per round rather than on the parent so the audit
    /// trail keeps each proposal's rationale -- "we offered Aug 27 because X, they declined;
    /// then Sep 3 because Y". Finalize copies the winning round's reason onto the request's
    /// <see cref="AppointmentChangeRequest.AdminReScheduleReason"/>.
    /// </summary>
    public virtual string? ProposedReason { get; protected set; }

    /// <summary>
    /// How many times this round's consent emails have been dispatched. Starts at 1 (the
    /// confirm that created the round) and increments on each resend. It is not merely a
    /// counter: the notification outbox keys on
    /// <c>SHA256(tenantId | recipientEmail | contextTag | packetKind)</c> and SILENTLY returns
    /// the existing row on a match, so without an attempt discriminator in the context tag a
    /// resend would vanish with no error.
    /// </summary>
    public virtual int SendAttempts { get; protected set; }

    /// <summary>Set when staff confirmed a different date; a superseded round no longer gates finalize.</summary>
    public virtual DateTime? SupersededAt { get; protected set; }

    // ---- Per-side consent, mirroring the parent's two symmetric slots ----
    // Side A = Patient + Applicant Attorney; Side B = Defense Attorney + Claim Examiner.

    /// <summary>Side A consent state. <c>NotRequired</c> when not solicited (no representative).</summary>
    public virtual ChangeRequestConsentStatus SideAConsentStatus { get; protected set; } = ChangeRequestConsentStatus.NotRequired;

    /// <summary>SHA256 hex of Side A's consent token; the raw token is never stored.</summary>
    public virtual string? SideAConsentTokenHash { get; protected set; }

    public virtual DateTime? SideAConsentExpiresAt { get; protected set; }

    public virtual DateTime? SideAConsentRespondedAt { get; protected set; }

    /// <summary>Email of Side A's representative who responded (audit; null on expiry-default).</summary>
    public virtual string? SideAConsentRespondedByEmail { get; protected set; }

    /// <summary>Side B consent state. <c>NotRequired</c> when not solicited (no representative).</summary>
    public virtual ChangeRequestConsentStatus SideBConsentStatus { get; protected set; } = ChangeRequestConsentStatus.NotRequired;

    /// <summary>SHA256 hex of Side B's consent token; the raw token is never stored.</summary>
    public virtual string? SideBConsentTokenHash { get; protected set; }

    public virtual DateTime? SideBConsentExpiresAt { get; protected set; }

    public virtual DateTime? SideBConsentRespondedAt { get; protected set; }

    /// <summary>Email of Side B's representative who responded (audit; null on expiry-default).</summary>
    public virtual string? SideBConsentRespondedByEmail { get; protected set; }

    protected ChangeRequestConsentRound()
    {
    }

    public ChangeRequestConsentRound(
        Guid id,
        Guid? tenantId,
        Guid appointmentChangeRequestId,
        int roundNumber,
        Guid proposedDoctorAvailabilityId,
        Guid? proposedByUserId,
        string? proposedReason = null)
    {
        Check.Positive(roundNumber, nameof(roundNumber));
        Check.NotDefaultOrNull<Guid>(proposedDoctorAvailabilityId, nameof(proposedDoctorAvailabilityId));
        Check.Length(proposedReason, nameof(proposedReason), AppointmentChangeRequestConsts.ReasonMaxLength);

        Id = id;
        TenantId = tenantId;
        AppointmentChangeRequestId = appointmentChangeRequestId;
        RoundNumber = roundNumber;
        ProposedDoctorAvailabilityId = proposedDoctorAvailabilityId;
        ProposedByUserId = proposedByUserId;
        ProposedReason = proposedReason;
        SendAttempts = 1;
    }

    // ---- Consent transitions (pure domain logic) ----

    /// <summary>
    /// Issue a consent token to a side, moving it to <see cref="ChangeRequestConsentStatus.Pending"/>.
    /// Only valid from <see cref="ChangeRequestConsentStatus.NotRequired"/> -- re-soliciting a
    /// side means opening a NEW round, never re-issuing on this one.
    /// </summary>
    public void IssueSideConsent(ChangeRequestSide side, string tokenHash, DateTime expiresAtUtc)
    {
        Check.NotNullOrWhiteSpace(tokenHash, nameof(tokenHash));
        EnsureSideStatus(side, ChangeRequestConsentStatus.NotRequired);
        SetSideTokenHash(side, tokenHash);
        SetSideExpiresAt(side, expiresAtUtc);
        SetSideStatus(side, ChangeRequestConsentStatus.Pending);
    }

    /// <summary>
    /// Replace a still-<see cref="ChangeRequestConsentStatus.Pending"/> side's token on a
    /// RESEND, and restart its 7-day window. Only valid from <c>Pending</c>: a side that has
    /// already ANSWERED must not be re-asked within the same round -- that needs a new round,
    /// which is what confirming a different date does.
    ///
    /// <para>The resend mints a new token rather than reusing the old one because only the
    /// token's SHA256 HASH is persisted, so the original raw token cannot be recovered to
    /// rebuild its URL (Adrian, 2026-08-05). The consequence is deliberate and accepted: the
    /// link in the SUPERSEDED email stops working, and the recipient must use the newest one.</para>
    /// </summary>
    public void ReissueSideConsent(ChangeRequestSide side, string tokenHash, DateTime expiresAtUtc)
    {
        Check.NotNullOrWhiteSpace(tokenHash, nameof(tokenHash));
        EnsureSideStatus(side, ChangeRequestConsentStatus.Pending);
        SetSideTokenHash(side, tokenHash);
        SetSideExpiresAt(side, expiresAtUtc);
    }

    /// <summary>
    /// Record a side's decision. Single-use: throws unless the side is currently
    /// <see cref="ChangeRequestConsentStatus.Pending"/>.
    /// </summary>
    public void RecordSideDecision(ChangeRequestSide side, bool approved, string? respondedByEmail, DateTime nowUtc)
    {
        if (SideConsentStatus(side) != ChangeRequestConsentStatus.Pending)
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.ChangeRequestConsentAlreadyResponded);
        }
        SetSideStatus(side, approved ? ChangeRequestConsentStatus.Approved : ChangeRequestConsentStatus.Rejected);
        SetSideRespondedAt(side, nowUtc);
        SetSideRespondedByEmail(side, respondedByEmail);
    }

    /// <summary>Mark a side expired (token lapsed). Treated as a No for the gate. No-op unless Pending.</summary>
    public void MarkSideExpired(ChangeRequestSide side, DateTime nowUtc)
    {
        if (SideConsentStatus(side) != ChangeRequestConsentStatus.Pending)
        {
            return;
        }
        SetSideStatus(side, ChangeRequestConsentStatus.Expired);
        SetSideRespondedAt(side, nowUtc);
    }

    /// <summary>True when the side's token is still pending and has passed its expiry.</summary>
    public bool IsSideExpired(ChangeRequestSide side, DateTime nowUtc)
    {
        var expiresAt = side == ChangeRequestSide.SideA ? SideAConsentExpiresAt : SideBConsentExpiresAt;
        return SideConsentStatus(side) == ChangeRequestConsentStatus.Pending
            && expiresAt.HasValue
            && expiresAt.Value <= nowUtc;
    }

    /// <summary>Current consent status for a side.</summary>
    public ChangeRequestConsentStatus SideConsentStatus(ChangeRequestSide side) =>
        side == ChangeRequestSide.SideA ? SideAConsentStatus : SideBConsentStatus;

    /// <summary>SHA256 hex of a side's consent token (null when the side was never solicited).</summary>
    public string? SideConsentTokenHash(ChangeRequestSide side) =>
        side == ChangeRequestSide.SideA ? SideAConsentTokenHash : SideBConsentTokenHash;

    /// <summary>
    /// Finalize gate for THIS round: every side whose consent was solicited
    /// (status != NotRequired) must be Approved. A side with no representative stays
    /// NotRequired and is satisfied -- there is nobody to ask.
    /// </summary>
    public bool AreAllRequiredSidesGranted() =>
        IsSideSatisfied(ChangeRequestSide.SideA) && IsSideSatisfied(ChangeRequestSide.SideB);

    /// <summary>
    /// Records that this round's consent emails were dispatched again. Deliberately touches
    /// NOTHING but the counter: the tokens must survive a resend because a party may already
    /// be holding the link from the first send.
    /// </summary>
    public void RegisterResend()
    {
        SendAttempts++;
    }

    /// <summary>
    /// Retire this round because staff confirmed a different date. Idempotent -- the FIRST
    /// supersede wins, so a double-submit cannot rewrite when the round was retired. Decisions
    /// already collected are deliberately preserved; they are the audit trail.
    /// </summary>
    public void Supersede(DateTime nowUtc)
    {
        SupersededAt ??= nowUtc;
    }

    private bool IsSideSatisfied(ChangeRequestSide side)
    {
        var status = SideConsentStatus(side);
        return status is ChangeRequestConsentStatus.NotRequired or ChangeRequestConsentStatus.Approved;
    }

    private void EnsureSideStatus(ChangeRequestSide side, ChangeRequestConsentStatus expected)
    {
        if (SideConsentStatus(side) != expected)
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.ChangeRequestConsentAlreadyResponded);
        }
    }

    private void SetSideStatus(ChangeRequestSide side, ChangeRequestConsentStatus value)
    {
        if (side == ChangeRequestSide.SideA) { SideAConsentStatus = value; } else { SideBConsentStatus = value; }
    }

    private void SetSideTokenHash(ChangeRequestSide side, string value)
    {
        if (side == ChangeRequestSide.SideA) { SideAConsentTokenHash = value; } else { SideBConsentTokenHash = value; }
    }

    private void SetSideExpiresAt(ChangeRequestSide side, DateTime value)
    {
        if (side == ChangeRequestSide.SideA) { SideAConsentExpiresAt = value; } else { SideBConsentExpiresAt = value; }
    }

    private void SetSideRespondedAt(ChangeRequestSide side, DateTime value)
    {
        if (side == ChangeRequestSide.SideA) { SideAConsentRespondedAt = value; } else { SideBConsentRespondedAt = value; }
    }

    private void SetSideRespondedByEmail(ChangeRequestSide side, string? value)
    {
        if (side == ChangeRequestSide.SideA) { SideAConsentRespondedByEmail = value; } else { SideBConsentRespondedByEmail = value; }
    }
}
