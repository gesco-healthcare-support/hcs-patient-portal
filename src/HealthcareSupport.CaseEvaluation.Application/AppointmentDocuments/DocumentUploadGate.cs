using System;
using System.Collections.Generic;
using System.Linq;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Data;
using HealthcareSupport.CaseEvaluation.Enums;
using Volo.Abp;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments;

/// <summary>
/// Phase 14 (2026-05-04) -- pure validation helpers for the upload
/// AppService surface. Each method maps to a strict-parity gate from
/// OLD <c>AppointmentDocumentDomain</c> /
/// <c>AppointmentJointDeclarationDomain</c>:
///
/// <list type="bullet">
///   <item><see cref="EnsureAppointmentApprovedAndNotPastDueDate"/> --
///     OLD <c>AppointmentDocumentDomain.cs</c>:90-107 (UpdateValidation).
///     Applies to package-doc + JDF paths. Ad-hoc upload is gateless.</item>
///   <item><see cref="EnsureAme"/> -- OLD JDF gate; only AME (and the
///     deferred AME-REVAL when introduced) accept JDF uploads.</item>
///   <item><see cref="EnsureCreatorIsAttorney"/> -- OLD JDF
///     booking-attorney-only check.</item>
///   <item><see cref="EnsureNotImmutable"/> -- OLD external-user write
///     gate against an Accepted document.</item>
/// </list>
///
/// <para><c>internal static</c> for unit-testability via
/// <c>InternalsVisibleTo</c>. Pure functions -- no DI -- so tests run
/// in microseconds and do not need the ABP integration harness (still
/// gated behind the Phase 4 license-checker test-host crash).</para>
/// </summary>
internal static class DocumentUploadGate
{
    /// <summary>
    /// Mirrors OLD <c>UpdateValidation</c> at
    /// <c>P:\PatientPortalOld\PatientAppointment.Domain\AppointmentRequestModule\AppointmentDocumentDomain.cs</c>:95-105.
    /// Throws <c>BusinessException(DocumentUploadAfterApproval)</c>
    /// when the appointment status is not Approved/RescheduleRequested,
    /// or <c>DocumentUploadAfterDueDate</c> when the appointment is
    /// past <see cref="Appointment.DueDate"/>. NULL <c>DueDate</c> is
    /// treated as "no due-date gate" (the appointment was created
    /// without one; the gate cannot fire). The OLD-verbatim error
    /// strings localize via the <c>Document:UploadAfterApproval</c> /
    /// <c>Document:UploadAfterDueDate</c> keys.
    /// </summary>
    public static void EnsureAppointmentApprovedAndNotPastDueDate(Appointment appointment)
    {
        if (appointment == null)
        {
            throw new ArgumentNullException(nameof(appointment));
        }
        var status = appointment.AppointmentStatus;
        if (status != AppointmentStatusType.Approved &&
            status != AppointmentStatusType.RescheduleRequested)
        {
            throw new BusinessException(
                CaseEvaluationDomainErrorCodes.DocumentUploadAfterApproval);
        }
        if (appointment.DueDate.HasValue && appointment.DueDate.Value < DateTime.UtcNow)
        {
            throw new BusinessException(
                CaseEvaluationDomainErrorCodes.DocumentUploadAfterDueDate);
        }
    }

    /// <summary>
    /// JDF-only gate: throws when the appointment is not an AME
    /// (or future AME-REVAL). Mirrors OLD JDF availability check.
    /// AME-REVAL deferred until that AppointmentType seed is added
    /// post-parity (NEW's seed at
    /// <c>CaseEvaluationSeedIds.AppointmentTypes</c> currently has
    /// only <c>Ame</c>).
    /// </summary>
    public static void EnsureAme(Guid appointmentTypeId)
    {
        if (appointmentTypeId != CaseEvaluationSeedIds.AppointmentTypes.Ame)
        {
            throw new BusinessException(
                CaseEvaluationDomainErrorCodes.JdfRequiresAmeAppointment);
        }
    }

    /// <summary>
    /// JDF-only gate: only the booking attorney (the user who created
    /// the appointment, where role == ApplicantAttorney or
    /// DefenseAttorney) can upload the JDF. Mirrors OLD's implicit
    /// UserClaim-driven write path. Requires the caller to pass the
    /// resolved role-name list because role lookup is async at the
    /// AppService boundary; this helper stays pure.
    /// </summary>
    public static void EnsureCreatorIsAttorney(
        Appointment appointment,
        Guid? currentUserId,
        IReadOnlyCollection<string> currentUserRoleNames)
    {
        if (appointment == null)
        {
            throw new ArgumentNullException(nameof(appointment));
        }
        Check.NotNull(currentUserRoleNames, nameof(currentUserRoleNames));

        if (!currentUserId.HasValue || appointment.IdentityUserId != currentUserId.Value)
        {
            throw new BusinessException(
                CaseEvaluationDomainErrorCodes.JdfUploaderMustBeBookingAttorney);
        }
        var isAttorney = currentUserRoleNames.Any(r =>
            string.Equals(r, "Applicant Attorney", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(r, "Defense Attorney", StringComparison.OrdinalIgnoreCase));
        if (!isAttorney)
        {
            throw new BusinessException(
                CaseEvaluationDomainErrorCodes.JdfUploaderMustBeBookingAttorney);
        }
    }

    /// <summary>
    /// External users cannot mutate an Accepted document. Internal
    /// users (clinic staff acting on behalf of the patient) bypass.
    /// Mirrors OLD's update-validation gate that disabled external
    /// re-upload after staff Accept.
    /// </summary>
    public static void EnsureNotImmutable(AppointmentDocument document, bool isInternalUser)
    {
        if (document == null)
        {
            throw new ArgumentNullException(nameof(document));
        }
        if (!isInternalUser && document.Status == DocumentStatus.Accepted)
        {
            throw new BusinessException(
                CaseEvaluationDomainErrorCodes.DocumentImmutableForExternalUser);
        }
    }

    /// <summary>
    /// Anonymous-upload gate: the supplied verification code must
    /// match the row's stored <see cref="AppointmentDocument.VerificationCode"/>
    /// AND the row must still be in <see cref="DocumentStatus.Pending"/>
    /// or <see cref="DocumentStatus.Rejected"/> (re-upload after
    /// rejection). Throws the OLD-verbatim "Un unauthorized user"
    /// error otherwise -- localized via the
    /// <c>Document:UnauthorizedVerificationCode</c> key.
    /// </summary>
    public static void EnsureVerificationCodeMatches(
        AppointmentDocument document,
        Guid suppliedCode)
    {
        if (document == null)
        {
            throw new BusinessException(
                CaseEvaluationDomainErrorCodes.DocumentUnauthorizedVerificationCode);
        }
        if (!document.VerificationCode.HasValue ||
            document.VerificationCode.Value != suppliedCode ||
            suppliedCode == Guid.Empty)
        {
            throw new BusinessException(
                CaseEvaluationDomainErrorCodes.DocumentUnauthorizedVerificationCode);
        }
        if (document.Status == DocumentStatus.Accepted)
        {
            throw new BusinessException(
                CaseEvaluationDomainErrorCodes.DocumentImmutableForExternalUser);
        }
    }
}
