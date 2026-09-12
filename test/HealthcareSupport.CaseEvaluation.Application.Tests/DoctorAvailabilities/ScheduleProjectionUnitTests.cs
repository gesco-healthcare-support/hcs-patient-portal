using System;
using System.Collections.Generic;
using System.Linq;
using HealthcareSupport.CaseEvaluation.Enums;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.DoctorAvailabilities;

/// <summary>
/// Phase 3 (2026-07-31) -- the staff schedule's capacity arithmetic and grouping.
///
/// <para>Pure: no DB, no ABP host. This is the piece that decides what staff believe is bookable, so
/// the full/over-capacity edges are pinned individually rather than by a loop. The booking picker
/// computes the same numbers inline but then HIDES full slots; the schedule must keep them.</para>
/// </summary>
public class ScheduleProjectionUnitTests
{
    private static readonly Guid SlotA = new("11111111-1111-4111-8111-111111111111");
    private static readonly Guid SlotB = new("22222222-2222-4222-8222-222222222222");

    private static DoctorAvailability NewSlot(Guid id, int capacity, DateTime date, int fromHour)
    {
        return new DoctorAvailability(
            id,
            locationId: Guid.NewGuid(),
            availableDate: date,
            fromTime: new TimeOnly(fromHour, 0),
            toTime: new TimeOnly(fromHour + 1, 0),
            bookingStatusId: BookingStatus.Available,
            capacity: capacity);
    }

    private static ScheduleAppointmentDto NewAppointment(string confirmation, AppointmentStatusType status)
    {
        return new ScheduleAppointmentDto
        {
            AppointmentId = Guid.NewGuid(),
            RequestConfirmationNumber = confirmation,
            PatientName = "Pat Example",
            Status = status,
        };
    }

    [Fact]
    public void An_empty_slot_reports_full_remaining_capacity()
    {
        var slots = new[] { NewSlot(SlotA, capacity: 3, new DateTime(2026, 8, 3), 9) };

        var result = ScheduleProjection.Build(
            slots,
            new Dictionary<Guid, List<ScheduleAppointmentDto>>(),
            new Dictionary<Guid, long>());

        var slot = result.ShouldHaveSingleItem();
        slot.ActiveCount.ShouldBe(0);
        slot.RemainingCapacity.ShouldBe(3);
        slot.Appointments.ShouldBeEmpty();
    }

    [Fact]
    public void A_full_slot_reports_zero_remaining_but_is_still_returned()
    {
        // The booking picker drops full slots; the schedule must NOT -- a booked slot is exactly
        // what staff came to look at.
        var slots = new[] { NewSlot(SlotA, capacity: 2, new DateTime(2026, 8, 3), 9) };
        var appointments = new Dictionary<Guid, List<ScheduleAppointmentDto>>
        {
            [SlotA] = new()
            {
                NewAppointment("A00001", AppointmentStatusType.Approved),
                NewAppointment("A00002", AppointmentStatusType.Pending),
            },
        };

        var result = ScheduleProjection.Build(slots, appointments, new Dictionary<Guid, long> { [SlotA] = 2 });

        var slot = result.ShouldHaveSingleItem();
        slot.ActiveCount.ShouldBe(2);
        slot.RemainingCapacity.ShouldBe(0);
        slot.Appointments.Count.ShouldBe(2);
    }

    [Fact]
    public void Over_capacity_never_reports_a_negative_remaining()
    {
        // Over-booking is reachable through manual capacity edits, and a negative would render as
        // nonsense in the UI.
        var slots = new[] { NewSlot(SlotA, capacity: 1, new DateTime(2026, 8, 3), 9) };

        var result = ScheduleProjection.Build(
            slots,
            new Dictionary<Guid, List<ScheduleAppointmentDto>>(),
            new Dictionary<Guid, long> { [SlotA] = 4 });

        result.ShouldHaveSingleItem().RemainingCapacity.ShouldBe(0);
    }

    [Fact]
    public void A_slot_with_no_count_entry_is_treated_as_empty()
    {
        // The batch count only returns slots that have appointments; a missing key means zero.
        var slots = new[] { NewSlot(SlotA, capacity: 3, new DateTime(2026, 8, 3), 9) };

        var result = ScheduleProjection.Build(
            slots,
            new Dictionary<Guid, List<ScheduleAppointmentDto>>(),
            new Dictionary<Guid, long> { [SlotB] = 2 });

        result.ShouldHaveSingleItem().ActiveCount.ShouldBe(0);
    }

    [Fact]
    public void Slots_are_ordered_by_date_then_start_time()
    {
        var slots = new[]
        {
            NewSlot(SlotB, capacity: 3, new DateTime(2026, 8, 4), 8),
            NewSlot(SlotA, capacity: 3, new DateTime(2026, 8, 3), 14),
            NewSlot(Guid.NewGuid(), capacity: 3, new DateTime(2026, 8, 3), 9),
        };

        var result = ScheduleProjection.Build(
            slots,
            new Dictionary<Guid, List<ScheduleAppointmentDto>>(),
            new Dictionary<Guid, long>());

        result.Select(s => (s.AvailableDate, s.FromTime)).ShouldBe(new[]
        {
            (new DateTime(2026, 8, 3), new TimeOnly(9, 0)),
            (new DateTime(2026, 8, 3), new TimeOnly(14, 0)),
            (new DateTime(2026, 8, 4), new TimeOnly(8, 0)),
        });
    }

    [Fact]
    public void Appointments_are_attached_to_their_own_slot_only()
    {
        var slots = new[]
        {
            NewSlot(SlotA, capacity: 3, new DateTime(2026, 8, 3), 9),
            NewSlot(SlotB, capacity: 3, new DateTime(2026, 8, 3), 10),
        };
        var appointments = new Dictionary<Guid, List<ScheduleAppointmentDto>>
        {
            [SlotB] = new() { NewAppointment("A00009", AppointmentStatusType.Approved) },
        };

        var result = ScheduleProjection.Build(slots, appointments, new Dictionary<Guid, long> { [SlotB] = 1 });

        result.Single(s => s.SlotId == SlotA).Appointments.ShouldBeEmpty();
        result.Single(s => s.SlotId == SlotB).Appointments.ShouldHaveSingleItem()
            .RequestConfirmationNumber.ShouldBe("A00009");
    }
}
