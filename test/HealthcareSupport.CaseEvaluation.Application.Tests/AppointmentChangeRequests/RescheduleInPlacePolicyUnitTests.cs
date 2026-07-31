using HealthcareSupport.CaseEvaluation.Enums;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

/// <summary>
/// B2 (2026-07-01) -- pure tests for <see cref="RescheduleInPlacePolicy"/>,
/// the in-place reschedule status-resolution used by the approval flow.
/// </summary>
public class RescheduleInPlacePolicyUnitTests
{
    [Fact]
    public void ResolveFinalizedStatus_RescheduleRequested_ReturnsApproved()
    {
        // Approved source returns from the transient RescheduleRequested to Approved.
        RescheduleInPlacePolicy.ResolveFinalizedStatus(AppointmentStatusType.RescheduleRequested)
            .ShouldBe(AppointmentStatusType.Approved);
    }

    [Fact]
    public void ResolveFinalizedStatus_Pending_StaysPending()
    {
        // Pending source (staff-initiated on a not-yet-approved appointment) stays Pending.
        RescheduleInPlacePolicy.ResolveFinalizedStatus(AppointmentStatusType.Pending)
            .ShouldBe(AppointmentStatusType.Pending);
    }

    // Every non-RescheduleRequested status is left unchanged so an in-place
    // reschedule never silently promotes or demotes an appointment's lifecycle.
    [Theory]
    [InlineData(AppointmentStatusType.Pending)]
    [InlineData(AppointmentStatusType.Approved)]
    [InlineData(AppointmentStatusType.Rejected)]
    [InlineData(AppointmentStatusType.CheckedIn)]
    [InlineData(AppointmentStatusType.CheckedOut)]
    [InlineData(AppointmentStatusType.Billed)]
    [InlineData(AppointmentStatusType.NoShow)]
    [InlineData(AppointmentStatusType.CancellationRequested)]
    [InlineData(AppointmentStatusType.CancelledNoBill)]
    [InlineData(AppointmentStatusType.CancelledLate)]
    [InlineData(AppointmentStatusType.RescheduledNoBill)]
    [InlineData(AppointmentStatusType.RescheduledLate)]
    public void ResolveFinalizedStatus_NonRescheduleRequested_Unchanged(AppointmentStatusType status)
    {
        RescheduleInPlacePolicy.ResolveFinalizedStatus(status).ShouldBe(status);
    }
}
