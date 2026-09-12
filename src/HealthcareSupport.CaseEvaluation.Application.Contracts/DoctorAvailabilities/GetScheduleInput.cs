using System;
using System.ComponentModel.DataAnnotations;

namespace HealthcareSupport.CaseEvaluation.DoctorAvailabilities;

/// <summary>
/// Range request for the staff schedule (phase 3, 2026-07-31).
///
/// <para><see cref="LocationId"/> is REQUIRED on purpose. The screen renders patient names, so
/// defaulting to every clinic at once would put more PHI on screen than the task needs; the existing
/// availabilities grid defaults to "All locations" and this deliberately does not.</para>
/// </summary>
public class GetScheduleInput
{
    [Required]
    public Guid LocationId { get; set; }

    /// <summary>Inclusive first day of the visible range (date part only).</summary>
    [Required]
    public DateTime FromDate { get; set; }

    /// <summary>Inclusive last day of the visible range (date part only).</summary>
    [Required]
    public DateTime ToDate { get; set; }
}
