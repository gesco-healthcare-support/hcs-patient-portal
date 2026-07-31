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
///    with <see cref="RequestStatus"/> = Pending. For reschedule,
///    <see cref="NewDoctorAvailabilityId"/> is the slot the user picked
///    and the parent appointment transitions
///    Approved -&gt; RescheduleRequested while the new slot is held in
///    Reserved status. For cancel, the parent appointment stays in
///    Approved while the change request is Pending.
///
/// 2. Staff Supervisor approves with a <see cref="CancellationOutcome"/>
///    (CancelledNoBill / CancelledLate for cancel; RescheduledNoBill /
///    RescheduledLate for reschedule), optionally overriding the slot.
///    On reschedule approve, a NEW Appointment row is created via
///    cascade-copy and the old appointment moves to the chosen
///    rescheduled outcome.
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

    /// <summary>The slot the user picked when submitting a reschedule. Null for cancel.</summary>
    public virtual Guid? NewDoctorAvailabilityId { get; set; }

    public virtual RequestStatusType RequestStatus { get; set; }

    /// <summary>Notes captured when supervisor rejects the request.</summary>
    [CanBeNull]
    public virtual string? RejectionNotes { get; set; }

    public virtual Guid? RejectedById { get; set; }

    public virtual Guid? ApprovedById { get; set; }

    /// <summary>
    /// Set when the supervisor overrode the user-picked slot during
    /// reschedule approval. Required when <see cref="AdminOverrideSlotId"/>
    /// is set and differs from <see cref="NewDoctorAvailabilityId"/>.
    /// </summary>
    [CanBeNull]
    public virtual string? AdminReScheduleReason { get; set; }

    /// <summary>Slot the supervisor chose if it differs from the user's pick.</summary>
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
            Check.NotNullOrWhiteSpace(reScheduleReason, nameof(reScheduleReason));
            Check.NotNull(newDoctorAvailabilityId, nameof(newDoctorAvailabilityId));
        }
        Check.Length(cancellationReason, nameof(cancellationReason), AppointmentChangeRequestConsts.ReasonMaxLength);
        Check.Length(reScheduleReason, nameof(reScheduleReason), AppointmentChangeRequestConsts.ReasonMaxLength);
        CancellationReason = cancellationReason;
        ReScheduleReason = reScheduleReason;
        NewDoctorAvailabilityId = newDoctorAvailabilityId;
        IsBeyondLimit = isBeyondLimit;
        RequestStatus = RequestStatusType.Pending;
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
