namespace HealthcareSupport.CaseEvaluation.DoctorAvailabilities;

/// <summary>
/// 2026-10-12 -- the current office's booking window as the server applies it to the CALLER, for
/// the booking calendar to mirror. The calendar used fixed copies (3 / 60 / 90) because the
/// system-parameter read is staff-only, so an office that changed its lead time or horizons saw
/// dates the server allows greyed out, or the reverse. These are the numbers
/// <c>BookingPolicyValidator</c> enforces; the server stays authoritative.
/// </summary>
public class BookingWindowDto
{
    /// <summary>The earliest bookable date is today (Pacific) plus this many days.</summary>
    public int LeadTimeDays { get; set; }

    /// <summary>
    /// The caller's own horizon: the appointment type's horizon for external users, the internal
    /// horizon for staff. Past it an external user is told to contact staff.
    /// </summary>
    public int MaxDays { get; set; }

    /// <summary>The absolute ceiling (the internal horizon). No role books beyond it.</summary>
    public int CeilingDays { get; set; }
}
