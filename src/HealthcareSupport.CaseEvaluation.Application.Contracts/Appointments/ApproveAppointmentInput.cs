using System.ComponentModel.DataAnnotations;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// Phase 12 (2026-05-04) -- input DTO for
/// <c>IAppointmentApprovalAppService.ApproveAppointmentAsync</c>.
///
/// <para>Captures the optional approver comment. OLD also required a responsible team member
/// (<c>P:\PatientPortalOld\PatientAppointment.Domain\AppointmentRequestModule\AppointmentDomain.cs</c>:560-566);
/// that was retired on 2026-11-02 (the office's email-routing decision): approval no longer
/// asks for one, and the existing <c>Appointment.PrimaryResponsibleUserId</c> values are kept
/// as history only. NEW exposes this on the approval
/// AppService while leaving the existing thin
/// <c>AppointmentsAppService.ApproveAsync(id)</c> intact for legacy callers.
/// The patient-match override (G-02-08, dropped 2026-06-01) is gone: OLD
/// never had an un-merge action -- its approve screen only displayed the
/// match read-only.</para>
/// </summary>
public class ApproveAppointmentInput
{
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
