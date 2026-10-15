using System;
using System.ComponentModel.DataAnnotations;
using HealthcareSupport.CaseEvaluation.Enums;
using JetBrains.Annotations;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

/// <summary>
/// Phase 17 (2026-05-04) -- input DTO for
/// <c>IAppointmentChangeRequestsApprovalAppService.ApproveRescheduleAsync</c>, which phase 4c
/// (2026-08-05) turned into FINALIZE: the date is no longer chosen here.
///
/// <para>4c REMOVED <c>OverrideSlotId</c> and <c>AdminReScheduleReason</c> from this input.
/// The slot now comes from the consent round staff confirmed via
/// <see cref="ConfirmRescheduleDateInput"/> -- the one both sides actually agreed to. Keeping
/// them here would create two sources of truth and let a finalize silently move the
/// appointment to a date nobody consented to. This is a BREAKING wire change, safe only
/// because 4b is unreleased and ships together with 4c.</para>
/// </summary>
public class ApproveRescheduleInput
{
    /// <summary>
    /// Supervisor-selected outcome bucket, chosen at finalize. Must be
    /// <see cref="AppointmentStatusType.RescheduledNoBill"/> or
    /// <see cref="AppointmentStatusType.RescheduledLate"/>.
    /// </summary>
    [Required]
    public AppointmentStatusType RescheduleOutcome { get; set; }

    /// <summary>
    /// Optional ABP optimistic-concurrency stamp. See
    /// <see cref="ApproveCancellationInput.ConcurrencyStamp"/>.
    /// </summary>
    [CanBeNull]
    public string? ConcurrencyStamp { get; set; }
}
