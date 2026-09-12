using System;
using System.Collections.Generic;
using System.Linq;

namespace HealthcareSupport.CaseEvaluation.DoctorAvailabilities;

/// <summary>
/// Builds the staff schedule's slot rows from slots, their appointments and the batch active-count
/// lookup (phase 3, 2026-07-31).
///
/// <para>Extracted as a pure static rather than left inline in the app service so the capacity
/// arithmetic is unit-testable without the ABP host -- the same reason
/// <c>BillingStatusWire</c> and the admin-hub helpers are pure. It decides what staff believe is
/// bookable, so it is the part worth pinning with tests.</para>
///
/// <para>Two rules differ deliberately from the booking picker
/// (<c>GetDoctorAvailabilityLookupAsync</c>), which computes the same numbers: a FULL slot is kept
/// rather than dropped, and <c>BookingStatusId</c> is ignored entirely. Staff came to see booked
/// time, and that column is not ground truth -- no code assigns
/// <see cref="Enums.BookingStatus.Booked"/>.</para>
/// </summary>
public static class ScheduleProjection
{
    /// <summary>
    /// Projects each slot with its occupancy and appointments, ordered by date then start time.
    /// </summary>
    /// <param name="slots">Slots in the requested range, for one location.</param>
    /// <param name="appointmentsBySlot">
    /// Non-terminal appointments grouped by <c>DoctorAvailabilityId</c>. Grouped by the caller
    /// because <see cref="ScheduleAppointmentDto"/> carries no slot id -- it is nested under its
    /// slot on the wire, so repeating the key would be redundant.
    /// </param>
    /// <param name="activeCounts">
    /// Result of <c>IAppointmentRepository.GetActiveCountsForSlotsAsync</c>. Only slots WITH
    /// appointments come back, so a missing key means zero.
    /// </param>
    public static List<ScheduleSlotDto> Build(
        IReadOnlyCollection<DoctorAvailability> slots,
        IReadOnlyDictionary<Guid, List<ScheduleAppointmentDto>> appointmentsBySlot,
        IReadOnlyDictionary<Guid, long> activeCounts)
    {
        ArgumentNullException.ThrowIfNull(slots);
        ArgumentNullException.ThrowIfNull(appointmentsBySlot);
        ArgumentNullException.ThrowIfNull(activeCounts);

        return slots
            .OrderBy(slot => slot.AvailableDate)
            .ThenBy(slot => slot.FromTime)
            .Select(slot =>
            {
                var active = activeCounts.TryGetValue(slot.Id, out var count) ? (int)count : 0;

                return new ScheduleSlotDto
                {
                    SlotId = slot.Id,
                    AvailableDate = slot.AvailableDate,
                    FromTime = slot.FromTime,
                    ToTime = slot.ToTime,
                    Capacity = slot.Capacity,
                    ActiveCount = active,
                    // Floored: over-booking is reachable by a manual capacity edit, and a negative
                    // remaining would render as nonsense.
                    RemainingCapacity = Math.Max(0, slot.Capacity - active),
                    Appointments = appointmentsBySlot.TryGetValue(slot.Id, out var appointments)
                        ? appointments
                        : new List<ScheduleAppointmentDto>(),
                };
            })
            .ToList();
    }
}
