using System.ComponentModel.DataAnnotations;
using HealthcareSupport.CaseEvaluation.Enums;
using JetBrains.Annotations;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

/// <summary>
/// Phase 17 (2026-05-04) -- input DTO for
/// <c>IAppointmentChangeRequestsApprovalAppService.ApproveCancellationAsync</c>.
/// Mirrors OLD's supervisor outcome-bucket selection at
/// <c>P:\PatientPortalOld\PatientAppointment.Domain\AppointmentRequestModule\AppointmentChangeRequestDomain.cs</c>:263-291
/// where the cancellation-approval path writes either
/// <c>CancelledNoBill</c> or <c>CancelledLate</c> onto the parent
/// appointment based on the supervisor's free-form choice.
/// </summary>
public class ApproveCancellationInput
{
    /// <summary>
    /// Supervisor-selected outcome bucket. Must be
    /// <see cref="AppointmentStatusType.CancelledNoBill"/> or
    /// <see cref="AppointmentStatusType.CancelledLate"/>; the
    /// validator throws
    /// <c>BusinessException(ChangeRequestInvalidCancellationOutcome)</c>
    /// otherwise.
    /// </summary>
    [Required]
    public AppointmentStatusType CancellationOutcome { get; set; }

    /// <summary>
    /// Optional ABP optimistic-concurrency stamp. When the client
    /// round-trips it, EF Core enforces that the row has not been
    /// updated by a concurrent supervisor; the AppService catches
    /// <c>AbpDbConcurrencyException</c> and re-throws as
    /// <c>BusinessException(ChangeRequestAlreadyHandled)</c>.
    /// </summary>
    [CanBeNull]
    public string? ConcurrencyStamp { get; set; }
}
