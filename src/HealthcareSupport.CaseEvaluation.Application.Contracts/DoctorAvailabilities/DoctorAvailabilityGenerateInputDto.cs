using HealthcareSupport.CaseEvaluation.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace HealthcareSupport.CaseEvaluation.DoctorAvailabilities;

/// <summary>
/// 2026-05-15 -- multi-axis slot generation input. Replaces the pre-rework
/// single-date / single-range / single-type shape. Semantics: for every
/// calendar date in [FromDate, ToDate] that matches one of
/// <see cref="SelectedDays"/> (Sunday=0 ... Saturday=6), for every
/// <see cref="TimeRanges"/> entry, produce one slot per per-range duration
/// block. Each generated slot inherits the input-level <see cref="Capacity"/>
/// (default 3 per the 2026-05-27 locked decision) and
/// <see cref="AppointmentTypeIds"/>. Empty <see cref="SelectedDays"/> means
/// "every weekday in the range"; empty <see cref="AppointmentTypeIds"/>
/// means "any type accepted" (loose mode).
/// </summary>
public class DoctorAvailabilityGenerateInputDto
{
    public DateTime FromDate { get; set; }

    public DateTime ToDate { get; set; }

    /// <summary>
    /// Weekday indices to include (0=Sunday, 6=Saturday). Empty or null
    /// treated as "all weekdays". Duplicates and out-of-range entries are
    /// rejected by the AppService.
    /// </summary>
    public List<int>? SelectedDays { get; set; }

    /// <summary>
    /// At least one time range. Ranges within the same input MUST NOT
    /// overlap. Each range's duration (per-range override or the
    /// input-level <see cref="AppointmentDurationMinutes"/>) must be &gt; 0
    /// and the range must satisfy <c>FromTime &lt; ToTime</c>.
    /// </summary>
    // No [MinLength(1)] -- the AppService validates with a localized
    // message (DoctorAvailability:AtLeastOneTimeRangeRequired). Mirrored
    // approach is used for Capacity.
    public List<TimeRangeDto> TimeRanges { get; set; } = new();

    public BookingStatus BookingStatusId { get; set; } = Enum.GetValues<BookingStatus>()[0];

    public Guid LocationId { get; set; }

    /// <summary>
    /// Permitted appointment types for every generated slot. Empty list =
    /// "any type accepted" (loose mode).
    /// </summary>
    public List<Guid> AppointmentTypeIds { get; set; } = new();

    /// <summary>
    /// Input-level default duration. Each <see cref="TimeRangeDto"/> may
    /// override via <see cref="TimeRangeDto.AppointmentDurationMinutes"/>.
    /// Must be &gt; 0.
    /// </summary>
    public int AppointmentDurationMinutes { get; set; } = 15;

    /// <summary>
    /// Max simultaneous appointments per generated slot. Default 3 (locked
    /// decision 2026-05-27); internal staff override per call. Validation
    /// (Capacity &gt;= 1) happens in the AppService with a localized message
    /// rather than the ABP DataAnnotation interceptor's generic
    /// AbpValidationException.
    /// </summary>
    public int Capacity { get; set; } = 3;
}

/// <summary>
/// 2026-05-15 -- one time band of a multi-axis slot generation. Multiple
/// ranges per generation enable "30-minute morning + 60-minute afternoon"
/// without two separate API calls.
/// </summary>
public class TimeRangeDto
{
    public TimeOnly FromTime { get; set; }

    public TimeOnly ToTime { get; set; }

    /// <summary>
    /// Optional per-range duration override. Falls back to the parent
    /// <see cref="DoctorAvailabilityGenerateInputDto.AppointmentDurationMinutes"/>
    /// when null.
    /// </summary>
    public int? AppointmentDurationMinutes { get; set; }
}
