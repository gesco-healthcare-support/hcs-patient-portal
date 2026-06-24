using HealthcareSupport.CaseEvaluation.Enums;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

/// <summary>
/// Phase 15 (2026-05-04) -- pure tests for
/// <see cref="CancellationRequestValidators"/>.
/// </summary>
public class CancellationRequestValidatorsUnitTests
{
    [Theory]
    [InlineData(AppointmentStatusType.Approved, true)]
    [InlineData(AppointmentStatusType.Pending, false)]
    [InlineData(AppointmentStatusType.Rejected, false)]
    [InlineData(AppointmentStatusType.NoShow, false)]
    [InlineData(AppointmentStatusType.CancelledNoBill, false)]
    [InlineData(AppointmentStatusType.CancelledLate, false)]
    [InlineData(AppointmentStatusType.RescheduleRequested, false)]
    [InlineData(AppointmentStatusType.RescheduledNoBill, false)]
    [InlineData(AppointmentStatusType.RescheduledLate, false)]
    [InlineData(AppointmentStatusType.CheckedIn, false)]
    [InlineData(AppointmentStatusType.CheckedOut, false)]
    [InlineData(AppointmentStatusType.Billed, false)]
    [InlineData(AppointmentStatusType.CancellationRequested, false)]
    public void CanRequestCancellation_AllowsOnlyApproved(AppointmentStatusType status, bool expected)
    {
        CancellationRequestValidators.CanRequestCancellation(status).ShouldBe(expected);
    }

    [Fact]
    public void IsWithinNoCancelWindow_SlotInsideWindow_True()
    {
        // 5 days out, threshold 7 -> inside the no-cancel window.
        var today = new DateTime(2026, 5, 4);
        var slot = today.AddDays(5);
        CancellationRequestValidators.IsWithinNoCancelWindow(slot, today, cancelTimeDays: 7).ShouldBeTrue();
    }

    [Fact]
    public void IsWithinNoCancelWindow_SlotOutsideWindow_False()
    {
        // 10 days out, threshold 7 -> safely outside.
        var today = new DateTime(2026, 5, 4);
        var slot = today.AddDays(10);
        CancellationRequestValidators.IsWithinNoCancelWindow(slot, today, cancelTimeDays: 7).ShouldBeFalse();
    }

    [Fact]
    public void IsWithinNoCancelWindow_ExactlyOnBoundary_FalseStrictLessThan()
    {
        // OLD line 87 uses strict less-than: a slot exactly
        // cancelTimeDays out is still cancellable. Pin that boundary.
        var today = new DateTime(2026, 5, 4);
        var slot = today.AddDays(7);
        CancellationRequestValidators.IsWithinNoCancelWindow(slot, today, cancelTimeDays: 7).ShouldBeFalse();
    }

    [Fact]
    public void IsWithinNoCancelWindow_SameDay_True()
    {
        var today = new DateTime(2026, 5, 4);
        CancellationRequestValidators.IsWithinNoCancelWindow(today, today, cancelTimeDays: 1).ShouldBeTrue();
    }

    [Fact]
    public void IsWithinNoCancelWindow_PastSlot_True()
    {
        // Slot already in the past -- definitely inside the no-cancel
        // window. Defensive against bad data; no separate "past slot"
        // error path needed.
        var today = new DateTime(2026, 5, 4);
        var slot = today.AddDays(-3);
        CancellationRequestValidators.IsWithinNoCancelWindow(slot, today, cancelTimeDays: 7).ShouldBeTrue();
    }

    [Fact]
    public void IsWithinNoCancelWindow_TimeComponent_IgnoredViaDate()
    {
        // The helper uses .Date on both sides so a slot earlier in
        // the same calendar day still counts as 0 days out, NOT -1.
        var today = new DateTime(2026, 5, 4, 14, 0, 0);
        var slot = new DateTime(2026, 5, 4, 9, 0, 0);
        CancellationRequestValidators.IsWithinNoCancelWindow(slot, today, cancelTimeDays: 1).ShouldBeTrue();
    }
}
