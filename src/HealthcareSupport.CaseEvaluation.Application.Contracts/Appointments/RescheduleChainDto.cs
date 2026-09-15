using System;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// Phase 4d (2026-08-05) -- where an appointment came from, when it was created by finalizing a
/// reschedule. Null on every appointment that was booked normally.
///
/// <para>The agreed DATE is deliberately absent: it is this appointment's own
/// <c>AppointmentDate</c>, already carried on the DTO and already shown at the top of the detail
/// page. Repeating it would be the block's least useful field.</para>
///
/// <para>The three timestamps are separate moments and none of them is the appointment date. Each
/// side agrees when it answers its own consent email, and staff may not finalize until later --
/// which is exactly why they are recorded rather than inferred from the appointment.</para>
/// </summary>
public class RescheduleChainDto
{
    /// <summary>The appointment this one replaced. Its row still exists, closed and billable.</summary>
    public Guid SourceAppointmentId { get; set; }

    /// <summary>
    /// The source's <c>RequestConfirmationNumber</c> -- what staff and parties actually recognise
    /// an appointment by, so the block reads "Rescheduled from A00036" rather than showing a Guid.
    /// </summary>
    public string? SourceRequestConfirmationNumber { get; set; }

    /// <summary>Side A (patient + applicant attorney) agreed. Null when that side was never solicited.</summary>
    public DateTime? SideAAgreedAt { get; set; }

    /// <summary>Side B (defense attorney + claim examiner) agreed. Null when that side was never solicited.</summary>
    public DateTime? SideBAgreedAt { get; set; }

    /// <summary>When staff finalized the reschedule -- generally later than both agreements.</summary>
    public DateTime? DecidedAt { get; set; }
}
