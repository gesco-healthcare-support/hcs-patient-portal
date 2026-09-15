using System;
using System.ComponentModel.DataAnnotations;
using JetBrains.Annotations;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

/// <summary>
/// Phase 4c (2026-08-05) -- input for
/// <c>IAppointmentChangeRequestsApprovalAppService.ConfirmRescheduleDateAsync</c>: the staff
/// member commits to a date and BOTH sides are asked to consent to it.
///
/// <para>Confirming is a separate step from PICKING because Adrian asked for one: "what if the
/// staff selects a date and then changes it immediately, in that case 2 different emails will
/// go out. I want a button where the staff can click to confirm the date so we know that the
/// staff meant select this date". Selecting a date in the calendar has NO server effect; this
/// input is what creates a consent round and sends.</para>
/// </summary>
public class ConfirmRescheduleDateInput
{
    /// <summary>
    /// The slot staff are committing to. Confirming the SAME slot again resends within the
    /// current round (same tokens); a DIFFERENT slot supersedes it and opens round N+1.
    /// </summary>
    [Required]
    public Guid DoctorAvailabilityId { get; set; }

    /// <summary>
    /// Optional note explaining the chosen date; copied onto the change-request row at
    /// finalize for the audit trail.
    /// </summary>
    [CanBeNull]
    [StringLength(AppointmentChangeRequestConsts.ReasonMaxLength)]
    public string? AdminReScheduleReason { get; set; }

    /// <summary>
    /// Optional ABP optimistic-concurrency stamp. See
    /// <see cref="ApproveCancellationInput.ConcurrencyStamp"/>.
    /// </summary>
    [CanBeNull]
    public string? ConcurrencyStamp { get; set; }
}
