using System;
using HealthcareSupport.CaseEvaluation.Enums;
using Volo.Abp;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// Phase 12 (2026-05-04) -- pure validation helpers for the approve /
/// reject AppService surface. Mirrors OLD's <c>UpdateValidation</c>
/// idempotency block
/// (<c>P:\PatientPortalOld\PatientAppointment.Domain\AppointmentRequestModule\AppointmentDomain.cs</c>:312-344)
/// for the two transitions Phase 12 actually exposes (Pending -> Approved,
/// Pending -> Rejected). Other illegal transitions (e.g., approving a
/// Rejected appointment) fall through to Session A's
/// <c>AppointmentManager</c> state machine, which surfaces
/// <c>CaseEvaluation:AppointmentInvalidTransition</c>.
///
/// <para><c>internal static</c> for unit-testability via
/// <c>InternalsVisibleTo</c> (mirrors the Phase 3 SystemParameters,
/// Phase 10 PasswordResetGate, Phase 18 TemplateVariableSubstitutor
/// patterns).</para>
/// </summary>
internal static class AppointmentApprovalValidator
{
    /// <summary>
    /// Validates the approve-appointment input + current entity state.
    /// Throws <c>BusinessException(AppointmentApprovalRequiresResponsibleUser)</c>
    /// when the responsible-user GUID is empty (mirrors OLD's UI-side
    /// disabled-Approve-button gate). Throws
    /// <c>BusinessException(AppointmentNotPendingForApproval)</c>
    /// when the appointment is already Approved -- localizes to OLD's
    /// "Appointment Already Approved" verbatim string. Returns silently
    /// when status is anything else; the manager's state machine
    /// handles all other illegal transitions.
    /// </summary>
    public static void EnsureApprovable(Appointment appointment, ApproveAppointmentInput input)
    {
        if (appointment == null)
        {
            throw new ArgumentNullException(nameof(appointment));
        }
        if (input == null)
        {
            throw new ArgumentNullException(nameof(input));
        }
        if (input.PrimaryResponsibleUserId == Guid.Empty)
        {
            throw new BusinessException(
                CaseEvaluationDomainErrorCodes.AppointmentApprovalRequiresResponsibleUser);
        }
        if (appointment.AppointmentStatus == AppointmentStatusType.Approved)
        {
            throw new BusinessException(
                CaseEvaluationDomainErrorCodes.AppointmentNotPendingForApproval);
        }
    }

    /// <summary>
    /// Validates the reject-appointment input + current entity state.
    /// Throws <c>BusinessException(AppointmentRejectionRequiresNotes)</c>
    /// when the rejection notes are null or whitespace (OLD UI required
    /// the textarea). Throws
    /// <c>BusinessException(AppointmentNotPendingForRejection)</c>
    /// when the appointment is already Rejected -- localizes to OLD's
    /// "Appointment Already Rejected" verbatim string.
    /// </summary>
    public static void EnsureRejectable(Appointment appointment, RejectAppointmentInput input)
    {
        if (appointment == null)
        {
            throw new ArgumentNullException(nameof(appointment));
        }
        if (input == null)
        {
            throw new ArgumentNullException(nameof(input));
        }
        if (string.IsNullOrWhiteSpace(input.Reason))
        {
            throw new BusinessException(
                CaseEvaluationDomainErrorCodes.AppointmentRejectionRequiresNotes);
        }
        if (appointment.AppointmentStatus == AppointmentStatusType.Rejected)
        {
            throw new BusinessException(
                CaseEvaluationDomainErrorCodes.AppointmentNotPendingForRejection);
        }
    }

    /// <summary>
    /// Decides whether the staff approver wants to "ignore the dedup
    /// match and treat this as a new patient". Returns true when the
    /// appointment had a candidate dedup match
    /// (<c>IsPatientAlreadyExist == true</c>) AND the staff supplied
    /// <c>OverridePatientMatch == true</c>. The actual patient-row
    /// split (creating a new <c>Patient</c> row + relinking the
    /// appointment) is downstream work in Session A's manager rewrite;
    /// Phase 12 just records the decision on the entity's
    /// <c>IsPatientAlreadyExist</c> flag.
    /// </summary>
    public static bool ShouldOverridePatientMatch(Appointment appointment, ApproveAppointmentInput input)
    {
        if (appointment == null || input == null)
        {
            return false;
        }
        return appointment.IsPatientAlreadyExist && input.OverridePatientMatch;
    }
}
