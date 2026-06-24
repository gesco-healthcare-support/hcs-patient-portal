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
    /// Throws when the supervisor overrode the user-picked slot
    /// (<paramref name="overrideSlotId"/> is set AND differs from
    /// <paramref name="userPickedSlotId"/>) but did not supply
    /// <paramref name="adminReason"/>. Returns the resolved
    /// new-slot id (override if set, user-picked otherwise) so the
    /// caller does not duplicate the null-coalesce logic.
    /// </summary>
    public static Guid ResolveNewSlotAndEnsureAdminReason(
        Guid? userPickedSlotId,
        Guid? overrideSlotId,
        string? adminReason)
    {
        if (!userPickedSlotId.HasValue || userPickedSlotId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "userPickedSlotId is required for reschedule approval.",
                nameof(userPickedSlotId));
        }

        if (!overrideSlotId.HasValue || overrideSlotId.Value == userPickedSlotId.Value)
        {
            // No override; supervisor accepts the user's pick.
            return userPickedSlotId.Value;
        }

        if (string.IsNullOrWhiteSpace(adminReason))
        {
            throw new BusinessException(
                CaseEvaluationDomainErrorCodes.ChangeRequestAdminReasonRequired);
        }
        return overrideSlotId.Value;
    }

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
