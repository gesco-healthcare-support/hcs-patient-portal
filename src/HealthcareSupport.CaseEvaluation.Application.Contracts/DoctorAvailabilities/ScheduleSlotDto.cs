using System;
using System.Collections.Generic;

namespace HealthcareSupport.CaseEvaluation.DoctorAvailabilities;

/// <summary>
/// One doctor-availability slot on the staff schedule, with its real occupancy and the appointments
/// in it (phase 3, 2026-07-31).
///
/// <para>Unlike the booking picker's <c>DoctorAvailabilityDto</c>, a slot appears here even when it
/// is FULL and regardless of <c>BookingStatusId</c> -- staff need to see booked time, not just
/// bookable time. Occupancy comes from counting non-terminal appointments, never from
/// <c>BookingStatusId</c>, which no code actually sets to <c>Booked</c>.</para>
/// </summary>
public class ScheduleSlotDto
{
    public Guid SlotId { get; set; }

    public DateTime AvailableDate { get; set; }

    public TimeOnly FromTime { get; set; }

    public TimeOnly ToTime { get; set; }

    /// <summary>How many appointments the slot allows (defaults to 3 on the entity).</summary>
    public int Capacity { get; set; }

    /// <summary>Non-terminal appointments currently in the slot.</summary>
    public int ActiveCount { get; set; }

    /// <summary>
    /// <c>Capacity - ActiveCount</c>, floored at zero. Zero means full, which the client renders as
    /// a distinct state from free.
    /// </summary>
    public int RemainingCapacity { get; set; }

    public List<ScheduleAppointmentDto> Appointments { get; set; } = new();
}
