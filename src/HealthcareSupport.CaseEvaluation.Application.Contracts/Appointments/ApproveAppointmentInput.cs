using System;
using System.ComponentModel.DataAnnotations;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// Phase 12 (2026-05-04) -- input DTO for
/// <c>IAppointmentApprovalAppService.ApproveAppointmentAsync</c>.
///
/// <para>Captures OLD's approval surface: the responsible team member
/// (<c>P:\PatientPortalOld\PatientAppointment.Domain\AppointmentRequestModule\AppointmentDomain.cs</c>:560-566)
/// plus the optional approver comment. NEW exposes this on the approval
/// AppService while leaving the existing thin
/// <c>AppointmentsAppService.ApproveAsync(id)</c> intact for legacy callers.
/// The patient-match override (G-02-08, dropped 2026-06-01) is gone: OLD
/// never had an un-merge action -- its approve screen only displayed the
/// match read-only.</para>
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
    /// A1 (2026-05-05) -- optional free-text comment captured on approve.
    /// Mirrors OLD's "Any comments?" textarea on the approve modal
    /// (`P:\PatientPortalOld\patientappointment-portal\src\app\components\
    /// appointment-request\appointments\view\appointment-view.component.html`:141-144).
    /// OLD persisted this to <c>Appointment.InternalUserComments</c> in the
    /// same PATCH that flipped the status; NEW preserves the same end state.
    /// No required-validator or max-length per OLD; the entity column caps
    /// at <see cref="AppointmentConsts.InternalUserCommentsMaxLength"/>.
    /// </summary>
    [StringLength(AppointmentConsts.InternalUserCommentsMaxLength)]
    public string? InternalUserComments { get; set; }
}
