using System;
using HealthcareSupport.CaseEvaluation.Enums;
using JetBrains.Annotations;
using Volo.Abp;
using Volo.Abp.Auditing;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

/// <summary>
/// User-initiated cancel or reschedule request on an Approved appointment.
/// Mirrors OLD's <c>AppointmentChangeRequest</c> table (Phase 1.5,
/// 2026-05-01). Lifecycle:
///
/// 1. External user (creator OR accessor with Edit access) submits a row
///    with <see cref="RequestStatus"/> = Pending. For reschedule the
///    requestor supplies a REASON ONLY (phase 4b, 2026-08-04) and the
///    parent appointment transitions Approved -&gt; RescheduleRequested;
///    no slot is held, because none has been chosen yet. Internal staff
///    filing a reschedule may still propose a slot up front, in which
///    case <see cref="NewDoctorAvailabilityId"/> is set and that slot is
///    held in Reserved status. For cancel, the parent appointment stays
///    in Approved while the change request is Pending.
///
/// 2. Staff Supervisor approves with a <see cref="CancellationOutcome"/>
///    (CancelledNoBill / CancelledLate for cancel; RescheduledNoBill /
///    RescheduledLate for reschedule), CHOOSING the new slot (or
///    overriding a staff-proposed one) via
///    <see cref="AdminOverrideSlotId"/>. On reschedule approve the SAME
///    appointment moves to the chosen slot in place (B2, 2026-07-01);
///    epic phase 4d replaces this with a new-appointment split.
///
/// 3. Supervisor rejects with <see cref="RejectionNotes"/> and the
///    parent appointment reverts to Approved (with new slot released
///    on reschedule).
/// </summary>
[Audited]
public class AppointmentChangeRequest : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public virtual Guid? TenantId { get; set; }

    public virtual Guid AppointmentId { get; protected set; }

    public virtual ChangeRequestType ChangeRequestType { get; protected set; }

    /// <summary>Required when <see cref="ChangeRequestType"/> = Cancel.</summary>
    [CanBeNull]
    public virtual string? CancellationReason { get; set; }

    /// <summary>Required when <see cref="ChangeRequestType"/> = Reschedule.</summary>
    [CanBeNull]
    public virtual string? ReScheduleReason { get; set; }

    /// <summary>
    /// A slot proposed at SUBMIT time. Null for cancel, and -- since phase 4b (2026-08-04) --
    /// normally null for reschedule too: the requestor supplies a reason only and staff choose
    /// the slot at approval (<see cref="AdminOverrideSlotId"/>). Retained because internal staff
    /// filing a reschedule DO pick immediately, and so a future requestor-side "suggested date"
    /// needs no migration.
    /// </summary>
    public virtual Guid? NewDoctorAvailabilityId { get; set; }

    /// <summary>
    /// Phase 6 (2026-08-08): settable only via <see cref="MarkDecided"/> or the constructor --
    /// see the remarks there for why this and the three properties below are locked together.
    /// </summary>
    public virtual RequestStatusType RequestStatus { get; protected set; }

    /// <summary>Notes captured when supervisor rejects the request.</summary>
    [CanBeNull]
    public virtual string? RejectionNotes { get; set; }

    public virtual Guid? RejectedById { get; protected set; }

    public virtual Guid? ApprovedById { get; protected set; }

    /// <summary>
    /// Set when the supervisor overrode the user-picked slot during
    /// reschedule approval. Required when <see cref="AdminOverrideSlotId"/>
    /// is set and differs from <see cref="NewDoctorAvailabilityId"/>.
    /// </summary>
    [CanBeNull]
    public virtual string? AdminReScheduleReason { get; set; }

    /// <summary>
    /// The slot staff scheduled onto at approval. Phase 4b (2026-08-04) widened this from
    /// "only when it differs from the requestor's pick" to "whenever staff chose a slot",
    /// because staff are now the primary picker and the row would otherwise record no slot
    /// at all. Compare with <see cref="NewDoctorAvailabilityId"/> to tell a genuine override
    /// (both set and different) from a first-and-only choice (this set, the other null).
    /// </summary>
    public virtual Guid? AdminOverrideSlotId { get; set; }

    /// <summary>
    /// When true, the supervisor approved a reschedule beyond the
    /// per-type max-time gate. Lifts the gate when the change is applied.
    /// </summary>
    public virtual bool IsBeyondLimit { get; set; }

    /// <summary>
    /// Outcome bucket the supervisor chose on approval. Maps to one of
    /// the four <see cref="AppointmentStatusType"/> terminal states
    /// (CancelledNoBill / CancelledLate / RescheduledNoBill /
    /// RescheduledLate) and is written onto the parent / old appointment.
    /// </summary>
    public virtual AppointmentStatusType? CancellationOutcome { get; set; }

    /// <summary>
    /// Phase 4d (2026-08-05) -- when staff DECIDED this request, stamped once as
    /// <see cref="RequestStatus"/> becomes Accepted or Rejected, on all four decision paths.
    ///
    /// <para>Distinct from the consent timestamps: both sides can agree and staff still not get to
    /// the request until later, so "when both parties agreed" and "when it was actually actioned"
    /// are different moments and the history needs both.</para>
    ///
    /// <para>Why a column rather than the inherited <c>LastModificationTime</c>: that reflects the
    /// LAST write of any kind, so any later edit silently relabels when the decision was made. A log
    /// entry that quietly becomes wrong is worse than one that is absent.</para>
    /// </summary>
    public virtual DateTime? DecidedAt { get; protected set; }

    // ---- Consent (2026-07-01 redesign): two symmetric side-consent slots ----
    // Side A = Patient + Applicant Attorney; Side B = Defense Attorney + Claim Examiner.
    // Party-initiated auto-grants the requestor's side and tokens the opposing side;
    // staff-initiated tokens both sides. The finalize gate passes when every side whose
    // consent was required (status != NotRequired) is Approved.

    /// <summary>Which side submitted this request (party-initiated); null when staff initiated.</summary>
    public virtual ChangeRequestSide? RequestingSide { get; protected set; }

    /// <summary>
    /// IdentityUser id of the submitter, persisted for audit + consent routing.
    /// ABP's audit <c>CreatorId</c> is the fallback.
    /// </summary>
    public virtual Guid? SubmittedByUserId { get; protected set; }

    /// <summary>Side A consent state. <c>NotRequired</c> when not solicited (gating off / no rep).</summary>
    public virtual ChangeRequestConsentStatus SideAConsentStatus { get; protected set; } = ChangeRequestConsentStatus.NotRequired;

    /// <summary>SHA256 hex of Side A's consent token; the raw token is never stored. Null when auto-granted / NotRequired.</summary>
    public virtual string? SideAConsentTokenHash { get; protected set; }

    public virtual DateTime? SideAConsentExpiresAt { get; protected set; }

    public virtual DateTime? SideAConsentRespondedAt { get; protected set; }

    /// <summary>Email of Side A's representative who responded (audit; null on auto-grant / expiry-default).</summary>
    public virtual string? SideAConsentRespondedByEmail { get; protected set; }

    /// <summary>Side B consent state. <c>NotRequired</c> when not solicited (gating off / no rep).</summary>
    public virtual ChangeRequestConsentStatus SideBConsentStatus { get; protected set; } = ChangeRequestConsentStatus.NotRequired;

    /// <summary>SHA256 hex of Side B's consent token; the raw token is never stored. Null when auto-granted / NotRequired.</summary>
    public virtual string? SideBConsentTokenHash { get; protected set; }

    public virtual DateTime? SideBConsentExpiresAt { get; protected set; }

    public virtual DateTime? SideBConsentRespondedAt { get; protected set; }

    /// <summary>Email of Side B's representative who responded (audit; null on auto-grant / expiry-default).</summary>
    public virtual string? SideBConsentRespondedByEmail { get; protected set; }

    protected AppointmentChangeRequest()
    {
    }

    public AppointmentChangeRequest(
        Guid id,
        Guid? tenantId,
        Guid appointmentId,
        ChangeRequestType changeRequestType,
        string? cancellationReason,
        string? reScheduleReason,
        Guid? newDoctorAvailabilityId,
        bool isBeyondLimit = false)
    {
        Id = id;
        TenantId = tenantId;
        AppointmentId = appointmentId;
        ChangeRequestType = changeRequestType;
        if (changeRequestType == ChangeRequestType.Cancel)
        {
            Check.NotNullOrWhiteSpace(cancellationReason, nameof(cancellationReason));
        }
        if (changeRequestType == ChangeRequestType.Reschedule)
        {
            // Phase 4b (2026-08-04): the slot is NO LONGER required. Date selection moved from
            // the requestor to internal staff, who supply it via ApproveRescheduleInput
            // .OverrideSlotId at approval, so a submitted request legitimately carries only a
            // reason. The reason therefore stays mandatory -- it is the only thing the requestor
            // now provides.
            Check.NotNullOrWhiteSpace(reScheduleReason, nameof(reScheduleReason));
        }
        Check.Length(cancellationReason, nameof(cancellationReason), AppointmentChangeRequestConsts.ReasonMaxLength);
        Check.Length(reScheduleReason, nameof(reScheduleReason), AppointmentChangeRequestConsts.ReasonMaxLength);
        CancellationReason = cancellationReason;
        ReScheduleReason = reScheduleReason;
        NewDoctorAvailabilityId = newDoctorAvailabilityId;
        IsBeyondLimit = isBeyondLimit;
        RequestStatus = RequestStatusType.Pending;
    }

    /// <summary>
    /// Records the staff decision on this request as ONE act: the outcome, who decided it, and when.
    ///
    /// <para>Phase 6 (2026-08-08). These three facts together ARE the legal record of a decision, so
    /// the four properties they touch are <c>protected set</c> and this is the only way in. Before
    /// this they were public and written in FIVE different places -- status and actor at the four
    /// approve/reject call sites, the timestamp at a fifth in <c>PersistChangeRequestAsync</c> --
    /// so nothing but convention stopped a future path recording "rejected" with no actor, or a
    /// status change that never stamped a time. Adrian: "This is logs for proper legal processes, we
    /// want them to be exact." Convention is not a guarantee; the type is.</para>
    ///
    /// <para>The timestamp is written ONCE. A re-decision updates the outcome and actor but leaves
    /// the original <see cref="DecidedAt"/> standing, because the first decision is the one that
    /// happened -- silently relabelling when it occurred is the exact failure this column was added
    /// to prevent (see its remarks).</para>
    /// </summary>
    /// <param name="outcome">
    /// <see cref="RequestStatusType.Accepted"/> or <see cref="RequestStatusType.Rejected"/>.
    /// </param>
    /// <param name="decidedById">The staff user who decided. Null only where no user context exists.</param>
    /// <param name="nowUtc">Decision time. MUST be UTC.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="outcome"/> is not a decision.</exception>
    /// <exception cref="ArgumentException"><paramref name="nowUtc"/> is not UTC.</exception>
    public void MarkDecided(RequestStatusType outcome, Guid? decidedById, DateTime nowUtc)
    {
        if (outcome is not (RequestStatusType.Accepted or RequestStatusType.Rejected))
        {
            // Pending is not a decision. Coercing it would let this method UN-decide a request and
            // erase who decided it and when.
            throw new ArgumentOutOfRangeException(
                nameof(outcome), outcome, "Only Accepted or Rejected is a decision.");
        }

        if (nowUtc.Kind != DateTimeKind.Utc)
        {
            // A local-kind timestamp on a legal record is ambiguous the moment it crosses a
            // boundary. AbpClockOptions.Kind is pinned to Utc as of 2026-08-27, so IClock.Now now
            // satisfies this on its own -- but the guard stays. It is a precondition on the value
            // the caller hands in, not a workaround for the clock, and EF still reads datetime2
            // back as Unspecified, so a re-read value can still arrive here unkinded.
            throw new ArgumentException("The decision time must be UTC.", nameof(nowUtc));
        }

        RequestStatus = outcome;
        if (outcome == RequestStatusType.Accepted)
        {
            ApprovedById = decidedById;
            RejectedById = null;
        }
        else
        {
            RejectedById = decidedById;
            ApprovedById = null;
        }

        DecidedAt ??= nowUtc;
    }

    // ---- Two-sided consent transitions (pure domain logic) ----

    /// <summary>Records submitter metadata: the party's side when party-initiated (else null), plus the user id.</summary>
    public void InitiateConsent(ChangeRequestSide? requestingSide, Guid submittedByUserId)
    {
        RequestingSide = requestingSide;
        SubmittedByUserId = submittedByUserId;
    }

    /// <summary>
    /// Grant a side without a token -- the requestor's own side (party-initiated), or a side
    /// with no representative. Only valid from <see cref="ChangeRequestConsentStatus.NotRequired"/>.
    /// </summary>
    public void AutoGrantSide(ChangeRequestSide side, DateTime nowUtc)
    {
        EnsureSideStatus(side, ChangeRequestConsentStatus.NotRequired);
        SetSideStatus(side, ChangeRequestConsentStatus.Approved);
        SetSideRespondedAt(side, nowUtc);
    }

    /// <summary>
    /// Issue a consent token to a side, moving it to <see cref="ChangeRequestConsentStatus.Pending"/>.
    /// Only valid from <see cref="ChangeRequestConsentStatus.NotRequired"/>.
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

    /// <summary>SHA256 hex of a side's consent token (null when auto-granted / NotRequired).</summary>
    public string? SideConsentTokenHash(ChangeRequestSide side) =>
        side == ChangeRequestSide.SideA ? SideAConsentTokenHash : SideBConsentTokenHash;

    /// <summary>
    /// Finalize gate: every side whose consent was required (status != NotRequired) must be
    /// Approved. Both NotRequired (gating off / no reps) also passes -- nothing to consent.
    /// </summary>
    public bool AreAllRequiredSidesGranted() =>
        IsSideSatisfied(ChangeRequestSide.SideA) && IsSideSatisfied(ChangeRequestSide.SideB);

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
