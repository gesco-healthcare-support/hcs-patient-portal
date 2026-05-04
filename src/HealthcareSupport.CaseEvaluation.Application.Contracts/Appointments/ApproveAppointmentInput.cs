using System;
using System.ComponentModel.DataAnnotations;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// Phase 12 (2026-05-04) -- input DTO for
/// <c>IAppointmentApprovalAppService.ApproveAppointmentAsync</c>.
///
/// <para>Mirrors OLD's "select responsible team member + (optional) override
/// patient match" approval surface
/// (<c>P:\PatientPortalOld\PatientAppointment.Domain\AppointmentRequestModule\AppointmentDomain.cs</c>:560-566).
/// OLD's UI exposes both fields on the same edit page; NEW exposes the
/// same shape on the new approval AppService while leaving the existing
/// thin <c>AppointmentsAppService.ApproveAsync(id)</c> intact for legacy
/// callers (Session A built the thin endpoint earlier in Phase 11).</para>
/// </summary>
public class ApproveAppointmentInput
{
    /// <summary>
    /// Internal staff user (Clinic Staff / Staff Supervisor / IT Admin) chosen
    /// as the primary responsible user for this appointment. Required -- the
    /// validator throws <c>BusinessException(AppointmentApprovalRequiresResponsibleUser)</c>
    /// when the default <see cref="Guid.Empty"/> is supplied, which mirrors
    /// OLD's behavior (the OLD edit page disabled the Approve button until
    /// the dropdown was populated).
    /// </summary>
    [Required]
    public Guid PrimaryResponsibleUserId { get; set; }

    /// <summary>
    /// True when the staff approver wants to ignore the dedup match (the
    /// 3-of-6-fields rule that <c>Appointment.IsPatientAlreadyExist</c>
    /// records at booking time) and treat this appointment as a new patient
    /// even though a candidate match was found. Mirrors OLD's "Link existing
    /// patient" vs "Create new patient" toggle on the approval page.
    /// Defaults to <c>false</c> = accept the dedup match (the safer default
    /// because a false-negative match leaks PHI across patients).
    /// </summary>
    public bool OverridePatientMatch { get; set; }
}
