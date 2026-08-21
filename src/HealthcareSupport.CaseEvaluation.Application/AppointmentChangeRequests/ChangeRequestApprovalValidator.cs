using System;
using HealthcareSupport.CaseEvaluation.Enums;
using Volo.Abp;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

/// <summary>
/// Phase 17 (2026-05-04) -- pure validation helpers for the change-
/// request approval AppService. Mirrors OLD's outcome-bucket gate
/// (<c>P:\PatientPortalOld\PatientAppointment.Domain\AppointmentRequestModule\AppointmentChangeRequestDomain.cs</c>:263-309)
/// + the supervisor's UI-side admin-reason gate.
///
/// <para><c>internal static</c> for unit-testability via
/// <c>InternalsVisibleTo</c> (mirrors the Phase 3 SystemParameters,
/// Phase 12 AppointmentApprovalValidator, Phase 14 DocumentUploadGate
/// patterns).</para>
/// </summary>
internal static class ChangeRequestApprovalValidator
{
    /// <summary>
    /// Throws when <paramref name="request"/>'s
    /// <see cref="AppointmentChangeRequest.RequestStatus"/> is not
    /// <see cref="RequestStatusType.Pending"/>. Pre-flight check the
    /// AppService runs before any state mutation so the supervisor
    /// sees the OLD-verbatim "This change request has already been
    /// processed" wording instead of the optimistic-concurrency
    /// generic "AbpDbConcurrencyException" on the second supervisor's
    /// click. Both gates raise the same code so callers do not need
    /// to branch.
    /// </summary>
    public static void EnsurePending(AppointmentChangeRequest request)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }
        if (request.RequestStatus != RequestStatusType.Pending)
        {
            throw new BusinessException(
                CaseEvaluationDomainErrorCodes.ChangeRequestAlreadyHandled);
        }
    }

    /// <summary>
    /// Throws when the cancellation-approval outcome is not
    /// <see cref="AppointmentStatusType.CancelledNoBill"/> or
    /// <see cref="AppointmentStatusType.CancelledLate"/>. The
    /// supervisor picks free-form per OLD parity (no auto-derive
    /// from <c>AppointmentCancelTime</c>); this gate stops the
    /// caller from supplying e.g. <c>Approved</c> or <c>Rejected</c>
    /// values that would corrupt the appointment lifecycle.
    /// </summary>
    public static void EnsureCancellationOutcome(AppointmentStatusType outcome)
    {
        if (outcome != AppointmentStatusType.CancelledNoBill &&
            outcome != AppointmentStatusType.CancelledLate)
        {
            throw new BusinessException(
                CaseEvaluationDomainErrorCodes.ChangeRequestInvalidCancellationOutcome);
        }
    }

    /// <summary>
    /// Throws when the reschedule-approval outcome is not
    /// <see cref="AppointmentStatusType.RescheduledNoBill"/> or
    /// <see cref="AppointmentStatusType.RescheduledLate"/>.
    /// </summary>
    public static void EnsureRescheduleOutcome(AppointmentStatusType outcome)
    {
        if (outcome != AppointmentStatusType.RescheduledNoBill &&
            outcome != AppointmentStatusType.RescheduledLate)
        {
            throw new BusinessException(
                CaseEvaluationDomainErrorCodes.ChangeRequestInvalidRescheduleOutcome);
        }
    }

    /// <summary>
    /// Resolves which slot a reschedule approval schedules onto, and enforces the
    /// admin-reason gate.
    ///
    /// <para>Phase 4b (2026-08-04) reshaped this. Date selection moved from the requestor
    /// to internal staff, so <paramref name="userPickedSlotId"/> is now normally ABSENT and
    /// the staff choice arrives as <paramref name="overrideSlotId"/> with nothing to
    /// override. The gate therefore distinguishes two cases:</para>
    /// <list type="bullet">
    /// <item>Nothing was proposed at submit -- the staff pick IS the slot, and no
    /// <paramref name="adminReason"/> is required, because no one is being overruled.</item>
    /// <item>A slot WAS proposed (internal staff filing a reschedule) and the approver picks a
    /// different one -- that is a genuine override and the reason is required.</item>
    /// </list>
    /// <para>With neither supplied there is nothing to schedule onto: raises
    /// <c>ChangeRequestNewSlotRequired</c>. Pre-4b this threw <see cref="ArgumentException"/>,
    /// which surfaced as an HTTP 500 rather than a corrective message.</para>
    /// </summary>
    public static Guid ResolveNewSlotAndEnsureAdminReason(
        Guid? userPickedSlotId,
        Guid? overrideSlotId,
        string? adminReason)
    {
        // Guid.Empty is treated as "not supplied" on both sides, matching the pre-4b coalescing.
        var proposedSlotId = Normalize(userPickedSlotId);
        var staffSlotId = Normalize(overrideSlotId);

        if (!proposedSlotId.HasValue)
        {
            if (!staffSlotId.HasValue)
            {
                throw new BusinessException(
                    CaseEvaluationDomainErrorCodes.ChangeRequestNewSlotRequired);
            }
            // Staff made the only choice -- not an override, so no admin reason is owed.
            return staffSlotId.Value;
        }

        if (!staffSlotId.HasValue || staffSlotId.Value == proposedSlotId.Value)
        {
            // Approver accepts the slot proposed at submit.
            return proposedSlotId.Value;
        }

        if (string.IsNullOrWhiteSpace(adminReason))
        {
            throw new BusinessException(
                CaseEvaluationDomainErrorCodes.ChangeRequestAdminReasonRequired);
        }
        return staffSlotId.Value;
    }

    /// <summary>
    /// True only when staff replaced a slot the requestor actually proposed -- i.e. BOTH ids are
    /// supplied and they differ. Phase 4b (2026-08-04): with the requestor no longer proposing a
    /// date, a naive "staff picked something different from null" test reports every approval as
    /// an override, and <c>ChangeRequestApprovedEmailHandler</c> then tells the requestor their
    /// request was "changed by our team" when they never named a date. Drives the email wording
    /// and the <c>IsAdminOverride</c> flag on the approved Eto, so it must mean exactly this.
    /// </summary>
    public static bool IsAdminOverride(Guid? proposedSlotId, Guid? staffSlotId)
    {
        var proposed = Normalize(proposedSlotId);
        var staff = Normalize(staffSlotId);
        return proposed.HasValue && staff.HasValue && proposed.Value != staff.Value;
    }

    /// <summary>
    /// The slot a reschedule was actually scheduled onto, read back from a persisted change
    /// request: the staff choice when present, else the slot proposed at submit. Phase 4b
    /// (2026-08-04): consumers must NOT gate this on
    /// <see cref="IsAdminOverride(System.Guid?, System.Guid?)"/>, because on the external path
    /// the staff choice is the only slot AND is not an override -- gating returned null, and
    /// null renders as an empty date.
    /// </summary>
    public static Guid? ResolveScheduledSlotId(Guid? adminOverrideSlotId, Guid? newDoctorAvailabilityId) =>
        Normalize(adminOverrideSlotId) ?? Normalize(newDoctorAvailabilityId);

    private static Guid? Normalize(Guid? slotId) =>
        slotId.HasValue && slotId.Value != Guid.Empty ? slotId : null;

    /// <summary>
    /// Throws when supervisor rejects without rejection notes.
    /// </summary>
    public static void EnsureRejectionNotes(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            throw new BusinessException(
                CaseEvaluationDomainErrorCodes.ChangeRequestRejectionRequiresNotes);
        }
    }
}
