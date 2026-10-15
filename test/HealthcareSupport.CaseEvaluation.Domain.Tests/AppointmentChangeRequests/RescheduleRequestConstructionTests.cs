using System;
using HealthcareSupport.CaseEvaluation.Enums;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

/// <summary>
/// Pins the reschedule change-request constructor invariants after phase 4b (2026-08-04),
/// which moved date selection from the requestor to internal staff. A Reschedule row is
/// now valid with NO slot -- the requestor supplies only a reason and staff choose the slot
/// at approval -- while the reason stays mandatory. Pure domain unit; no DB.
/// </summary>
public class RescheduleRequestConstructionTests
{
    private static AppointmentChangeRequest NewReschedule(
        string? reason = "Patient is travelling",
        Guid? slotId = null) =>
        new(
            id: Guid.NewGuid(),
            tenantId: Guid.NewGuid(),
            appointmentId: Guid.NewGuid(),
            changeRequestType: ChangeRequestType.Reschedule,
            cancellationReason: null,
            reScheduleReason: reason,
            newDoctorAvailabilityId: slotId);

    [Fact]
    public void Reschedule_with_a_reason_and_no_slot_is_valid()
    {
        // Phase 4b: the requestor no longer picks a date, so a null slot is the normal case.
        var request = NewReschedule();

        request.ChangeRequestType.ShouldBe(ChangeRequestType.Reschedule);
        request.NewDoctorAvailabilityId.ShouldBeNull();
        request.ReScheduleReason.ShouldBe("Patient is travelling");
        request.RequestStatus.ShouldBe(RequestStatusType.Pending);
    }

    [Fact]
    public void Reschedule_still_accepts_a_slot_when_one_is_supplied()
    {
        // Staff filing a reschedule pick immediately, and the field is retained for a
        // future "suggested date" from the requestor.
        var slotId = Guid.NewGuid();

        var request = NewReschedule(slotId: slotId);

        request.NewDoctorAvailabilityId.ShouldBe(slotId);
    }

    [Fact]
    public void Reschedule_without_a_reason_is_rejected()
    {
        // The reason is the ONLY thing the requestor now supplies, so it stays required.
        Should.Throw<ArgumentException>(() => NewReschedule(reason: null));
        Should.Throw<ArgumentException>(() => NewReschedule(reason: "   "));
    }

    [Fact]
    public void Cancel_still_requires_a_cancellation_reason()
    {
        // Guards against the 4b relaxation leaking into the cancel branch.
        Should.Throw<ArgumentException>(() => new AppointmentChangeRequest(
            id: Guid.NewGuid(),
            tenantId: Guid.NewGuid(),
            appointmentId: Guid.NewGuid(),
            changeRequestType: ChangeRequestType.Cancel,
            cancellationReason: null,
            reScheduleReason: null,
            newDoctorAvailabilityId: null));
    }
}
