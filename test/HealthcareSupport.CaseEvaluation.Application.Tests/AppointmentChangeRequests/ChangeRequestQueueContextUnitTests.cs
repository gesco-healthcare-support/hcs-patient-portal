using System;
using System.Collections.Generic;
using HealthcareSupport.CaseEvaluation.Enums;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

/// <summary>
/// Phase 4b (2026-08-04) -- pure unit tests for the approval-queue context projection. Pins the
/// rules the supervisor UI depends on: location/type always populated so the availability
/// calendar can load, and a requested date shown ONLY when the requestor actually proposed one.
/// </summary>
public class ChangeRequestQueueContextUnitTests
{
    private static readonly Guid AppointmentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid LocationId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid AppointmentTypeId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid SlotId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid RoundSlotId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid ChangeRequestId = Guid.Parse("66666666-6666-6666-6666-666666666666");

    private static AppointmentChangeRequestDto NewDto(Guid? proposedSlotId = null) => new()
    {
        Id = ChangeRequestId,
        AppointmentId = AppointmentId,
        NewDoctorAvailabilityId = proposedSlotId,
        ChangeRequestType = ChangeRequestType.Reschedule,
    };

    private static Dictionary<Guid, ChangeRequestQueueContext.ConsentRoundContext> RoundFor(
        ChangeRequestConsentStatus sideA = ChangeRequestConsentStatus.Pending,
        ChangeRequestConsentStatus sideB = ChangeRequestConsentStatus.Pending,
        int roundNumber = 2,
        int sendAttempts = 1,
        Guid? slotId = null) =>
        new()
        {
            [ChangeRequestId] = new(
                roundNumber, slotId ?? RoundSlotId, sideA, sideB, sendAttempts),
        };

    private static Dictionary<Guid, ChangeRequestQueueContext.AppointmentContext> Appointments() =>
        new() { [AppointmentId] = new(LocationId, AppointmentTypeId) };

    private static Dictionary<Guid, ChangeRequestQueueContext.SlotContext> Slots() =>
        new()
        {
            [SlotId] = new(new DateTime(2026, 8, 13), new TimeOnly(9, 30)),
            [RoundSlotId] = new(new DateTime(2026, 8, 27), new TimeOnly(10, 30)),
        };

    private static Dictionary<Guid, ChangeRequestQueueContext.ConsentRoundContext> NoRounds() =>
        new();

    [Fact]
    public void Location_and_type_are_populated_so_the_calendar_can_load()
    {
        var dto = NewDto();

        ChangeRequestQueueContext.Apply(new[] { dto }, Appointments(), Slots(), NoRounds());

        dto.AppointmentLocationId.ShouldBe(LocationId);
        dto.AppointmentTypeId.ShouldBe(AppointmentTypeId);
    }

    [Fact]
    public void A_row_with_no_proposed_slot_gets_no_requested_date()
    {
        // The normal external case after 4b. Showing a date here would be inventing one.
        var dto = NewDto(proposedSlotId: null);

        ChangeRequestQueueContext.Apply(new[] { dto }, Appointments(), Slots(), NoRounds());

        dto.RequestedSlotDate.ShouldBeNull();
        dto.RequestedSlotFromTime.ShouldBeNull();
    }

    [Fact]
    public void A_row_with_a_proposed_slot_gets_its_date_and_start_time()
    {
        var dto = NewDto(proposedSlotId: SlotId);

        ChangeRequestQueueContext.Apply(new[] { dto }, Appointments(), Slots(), NoRounds());

        dto.RequestedSlotDate.ShouldBe(new DateTime(2026, 8, 13));
        dto.RequestedSlotFromTime.ShouldBe("09:30");
    }

    [Fact]
    public void A_proposed_slot_that_no_longer_exists_leaves_the_date_unknown()
    {
        // Degrade to "unknown", never to a wrong date.
        var dto = NewDto(proposedSlotId: Guid.NewGuid());

        ChangeRequestQueueContext.Apply(new[] { dto }, Appointments(), Slots(), NoRounds());

        dto.RequestedSlotDate.ShouldBeNull();
        dto.RequestedSlotFromTime.ShouldBeNull();
    }

    [Fact]
    public void A_missing_appointment_leaves_context_null_rather_than_zeroed()
    {
        var dto = NewDto();

        ChangeRequestQueueContext.Apply(
            new[] { dto },
            new Dictionary<Guid, ChangeRequestQueueContext.AppointmentContext>(),
            Slots(),
            NoRounds());

        dto.AppointmentLocationId.ShouldBeNull();
        dto.AppointmentTypeId.ShouldBeNull();
    }

    [Fact]
    public void Every_row_in_the_batch_is_projected()
    {
        var withSlot = NewDto(proposedSlotId: SlotId);
        var withoutSlot = NewDto();

        ChangeRequestQueueContext.Apply(new[] { withSlot, withoutSlot }, Appointments(), Slots(), NoRounds());

        withSlot.RequestedSlotFromTime.ShouldBe("09:30");
        withoutSlot.RequestedSlotFromTime.ShouldBeNull();
        withoutSlot.AppointmentLocationId.ShouldBe(LocationId);
    }

    [Fact]
    public void A_row_with_no_consent_round_leaves_every_current_round_field_null()
    {
        // This is exactly how a freshly submitted reschedule arrives, and it is the signal the
        // approval modal reads as "staff still need to pick a date".
        var dto = NewDto();

        ChangeRequestQueueContext.Apply(new[] { dto }, Appointments(), Slots(), NoRounds());

        dto.CurrentConsentRoundNumber.ShouldBeNull();
        dto.CurrentRoundProposedSlotId.ShouldBeNull();
        dto.CurrentRoundProposedDate.ShouldBeNull();
        dto.CurrentRoundProposedFromTime.ShouldBeNull();
        dto.CurrentRoundSideAStatus.ShouldBeNull();
        dto.CurrentRoundSideBStatus.ShouldBeNull();
        dto.CurrentRoundSendAttempts.ShouldBeNull();
    }

    [Fact]
    public void A_row_with_a_consent_round_gets_its_date_statuses_and_attempt_count()
    {
        var dto = NewDto();

        ChangeRequestQueueContext.Apply(
            new[] { dto },
            Appointments(),
            Slots(),
            RoundFor(
                sideA: ChangeRequestConsentStatus.Approved,
                sideB: ChangeRequestConsentStatus.Pending,
                roundNumber: 2,
                sendAttempts: 3));

        dto.CurrentConsentRoundNumber.ShouldBe(2);
        dto.CurrentRoundProposedSlotId.ShouldBe(RoundSlotId);
        dto.CurrentRoundProposedDate.ShouldBe(new DateTime(2026, 8, 27));
        dto.CurrentRoundProposedFromTime.ShouldBe("10:30");
        dto.CurrentRoundSideAStatus.ShouldBe(ChangeRequestConsentStatus.Approved);
        dto.CurrentRoundSideBStatus.ShouldBe(ChangeRequestConsentStatus.Pending);
        dto.CurrentRoundSendAttempts.ShouldBe(3);
    }

    [Fact]
    public void The_round_date_is_independent_of_the_requestors_proposal()
    {
        // The two are different questions: what the requestor asked for at submit, and what
        // staff confirmed. Conflating them is how a party gets asked about the wrong date.
        var dto = NewDto(proposedSlotId: SlotId);

        ChangeRequestQueueContext.Apply(new[] { dto }, Appointments(), Slots(), RoundFor());

        dto.RequestedSlotDate.ShouldBe(new DateTime(2026, 8, 13));
        dto.CurrentRoundProposedDate.ShouldBe(new DateTime(2026, 8, 27));
    }

    [Fact]
    public void A_round_slot_that_no_longer_exists_leaves_the_round_date_unknown_but_keeps_its_statuses()
    {
        // Degrade to "unknown date", never to a wrong one -- and never lose where consent stands.
        var dto = NewDto();

        ChangeRequestQueueContext.Apply(
            new[] { dto }, Appointments(), Slots(), RoundFor(slotId: Guid.NewGuid()));

        dto.CurrentRoundProposedDate.ShouldBeNull();
        dto.CurrentRoundProposedFromTime.ShouldBeNull();
        dto.CurrentConsentRoundNumber.ShouldBe(2);
        dto.CurrentRoundSideAStatus.ShouldBe(ChangeRequestConsentStatus.Pending);
    }

    [Theory]
    [InlineData(0, 0, "00:00")]
    [InlineData(9, 5, "09:05")]
    [InlineData(13, 45, "13:45")]
    public void FromTime_is_zero_padded_24_hour_to_match_the_calendar(int hour, int minute, string expected)
    {
        // Must equal DoctorAvailabilityDto.fromTime's shape, or the picked and requested times
        // cannot be compared in the UI.
        ChangeRequestQueueContext.FormatFromTime(new TimeOnly(hour, minute)).ShouldBe(expected);
    }
}
