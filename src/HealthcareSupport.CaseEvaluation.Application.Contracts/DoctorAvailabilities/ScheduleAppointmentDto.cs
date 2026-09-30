using System;
using HealthcareSupport.CaseEvaluation.Enums;

namespace HealthcareSupport.CaseEvaluation.DoctorAvailabilities;

/// <summary>
/// One appointment occupying a slot on the staff schedule (phase 3, 2026-07-31).
///
/// <para>Deliberately VIEW-AGNOSTIC: it carries the raw status rather than a colour, css class or
/// "requested"/"booked" label, so the calendar library can be swapped for a hand-built grid without
/// touching the server. The frontend classifies from <see cref="Status"/>.</para>
///
/// <para>INTERNAL ONLY -- <see cref="PatientName"/> is PHI. The endpoint that returns this is gated
/// by <c>CaseEvaluation.DoctorAvailabilities</c> and there is no external surface for it.</para>
/// </summary>
public class ScheduleAppointmentDto
{
    /// <summary>Target of the chip's click-through to <c>/appointments/view/:id</c>.</summary>
    public Guid AppointmentId { get; set; }

    /// <summary><c>A</c>+5 digits. Shown on the chip so staff can match it to a case.</summary>
    public string RequestConfirmationNumber { get; set; } = string.Empty;

    public string PatientName { get; set; } = string.Empty;

    /// <summary>
    /// Raw lifecycle status. The client decides what counts as requested versus booked; only
    /// non-terminal appointments ever appear here.
    /// </summary>
    public AppointmentStatusType Status { get; set; }
}
