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

    // ---- Group D (2026-06-09): opposing-side consent ----

    /// <summary>Which side submitted this request; the opposing side's consent is solicited.</summary>
    public virtual ChangeRequestSide? RequestingSide { get; protected set; }

    /// <summary>
    /// IdentityUser id of the submitter, persisted so consent routing can
    /// determine the opposing side. ABP's audit <c>CreatorId</c> is the fallback.
    /// </summary>
    public virtual Guid? SubmittedByUserId { get; protected set; }

    /// <summary>Opposing-side consent state. <c>NotRequired</c> when consent gating is off.</summary>
    public virtual ChangeRequestConsentStatus ConsentStatus { get; protected set; } = ChangeRequestConsentStatus.NotRequired;

    /// <summary>SHA256 hex of the consent token; the raw token is never stored. Null when NotRequired.</summary>
    public virtual string? ConsentTokenHash { get; protected set; }

    /// <summary>UTC expiry of the consent token (issue + 7 days).</summary>
    public virtual DateTime? ConsentExpiresAt { get; protected set; }

    /// <summary>UTC timestamp the opposing side responded (or the request was marked Expired).</summary>
    public virtual DateTime? ConsentRespondedAt { get; protected set; }

    /// <summary>Email of the opposing-side representative who responded (audit; null on expiry-default).</summary>
    public virtual string? ConsentRespondedByEmail { get; protected set; }

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

    // ---- Group D consent transitions (pure domain logic) ----

    /// <summary>
    /// Issue opposing-side consent: records the token hash + submitting side and
    /// moves consent to <see cref="ChangeRequestConsentStatus.Pending"/>. Only
    /// valid from <see cref="ChangeRequestConsentStatus.NotRequired"/>.
    /// </summary>
    public void IssueConsent(
        string tokenHash,
        ChangeRequestSide requestingSide,
        Guid submittedByUserId,
        DateTime expiresAtUtc)
    {
        Check.NotNullOrWhiteSpace(tokenHash, nameof(tokenHash));
        if (ConsentStatus != ChangeRequestConsentStatus.NotRequired)
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.ChangeRequestConsentAlreadyResponded);
        }
        RequestingSide = requestingSide;
        SubmittedByUserId = submittedByUserId;
        ConsentTokenHash = tokenHash;
        ConsentExpiresAt = expiresAtUtc;
        ConsentStatus = ChangeRequestConsentStatus.Pending;
    }

    /// <summary>
    /// Record the opposing side's decision. Single-use: throws
    /// <c>ChangeRequestConsentAlreadyResponded</c> unless currently
    /// <see cref="ChangeRequestConsentStatus.Pending"/>.
    /// </summary>
    public void RecordConsentDecision(bool approved, string? respondedByEmail, DateTime nowUtc)
    {
        if (ConsentStatus != ChangeRequestConsentStatus.Pending)
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.ChangeRequestConsentAlreadyResponded);
        }
        ConsentStatus = approved ? ChangeRequestConsentStatus.Approved : ChangeRequestConsentStatus.Rejected;
        ConsentRespondedAt = nowUtc;
        ConsentRespondedByEmail = respondedByEmail;
    }

    /// <summary>
    /// Mark consent expired (token lapsed). Treated as a No for the supervisor
    /// gate; staff are notified. No-op unless currently
    /// <see cref="ChangeRequestConsentStatus.Pending"/>.
    /// </summary>
    public void MarkConsentExpired(DateTime nowUtc)
    {
        if (ConsentStatus != ChangeRequestConsentStatus.Pending)
        {
            return;
        }
        ConsentStatus = ChangeRequestConsentStatus.Expired;
        ConsentRespondedAt = nowUtc;
    }

    /// <summary>True when the consent token is still pending and has passed its expiry.</summary>
    public bool IsConsentExpired(DateTime nowUtc) =>
        ConsentStatus == ChangeRequestConsentStatus.Pending
        && ConsentExpiresAt.HasValue
        && ConsentExpiresAt.Value <= nowUtc;

    /// <summary>True when the opposing side has granted consent (the supervisor gate passes).</summary>
    public bool IsConsentGranted() => ConsentStatus == ChangeRequestConsentStatus.Approved;
}
