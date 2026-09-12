using HealthcareSupport.CaseEvaluation.Enums;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// 2026-06-14 -- pure unit tests for <see cref="StatusPillPolicy"/>, the
/// 14-status -> 6-pill bucketization used by the dashboard donut. Keeps the
/// backend breakdown in lockstep with the Angular appointmentStatusToPill util.
/// </summary>
public class StatusPillPolicyUnitTests
{
    [Theory]
    [InlineData(AppointmentStatusType.Pending, StatusPillPolicy.Pending)]
    [InlineData(AppointmentStatusType.InfoRequested, StatusPillPolicy.InfoRequested)]
    [InlineData(AppointmentStatusType.Approved, StatusPillPolicy.Approved)]
    [InlineData(AppointmentStatusType.Rejected, StatusPillPolicy.Rejected)]
    [InlineData(AppointmentStatusType.CancelledNoBill, StatusPillPolicy.Cancelled)]
    [InlineData(AppointmentStatusType.CancelledLate, StatusPillPolicy.Cancelled)]
    [InlineData(AppointmentStatusType.RescheduledNoBill, StatusPillPolicy.Rescheduled)]
    [InlineData(AppointmentStatusType.RescheduledLate, StatusPillPolicy.Rescheduled)]
    // Phase 5 (2026-08-07): a pill each, using the business's own long-standing
    // names. NOT folded into one bucket -- Adrian: "I do not want to invent new
    // names, these are long used names throughout the business".
    [InlineData(AppointmentStatusType.NoShow, StatusPillPolicy.NoShow)]
    [InlineData(AppointmentStatusType.NotSeen, StatusPillPolicy.NotSeen)]
    public void ToPill_MapsActiveStatusesToTheirPill(AppointmentStatusType status, string expectedPill)
    {
        StatusPillPolicy.ToPill(status).ShouldBe(expectedPill);
    }

    [Theory]
    [InlineData(AppointmentStatusType.CheckedIn)]
    [InlineData(AppointmentStatusType.CheckedOut)]
    [InlineData(AppointmentStatusType.Billed)]
    [InlineData(AppointmentStatusType.RescheduleRequested)]
    [InlineData(AppointmentStatusType.CancellationRequested)]
    public void ToPill_ReturnsNullForStatusesWithoutADonutPill(AppointmentStatusType status)
    {
        StatusPillPolicy.ToPill(status).ShouldBeNull();
    }

    [Theory]
    [InlineData(AppointmentStatusType.NoShow)]
    [InlineData(AppointmentStatusType.NotSeen)]
    public void ToPill_DoesNotReportAnAttendanceOutcomeAsCancelled(AppointmentStatusType status)
    {
        // The Angular util used to map NoShow onto the Cancelled pill while this
        // policy returned null -- the two disagreed despite both claiming to
        // mirror each other. Pinned on the backend side so they cannot drift
        // back together into the wrong answer.
        StatusPillPolicy.ToPill(status).ShouldNotBe(StatusPillPolicy.Cancelled);
    }

    [Fact]
    public void DonutOrder_IsThePillsInPrototypeOrder()
    {
        StatusPillPolicy.DonutOrder.ShouldBe(new[]
        {
            StatusPillPolicy.Pending,
            StatusPillPolicy.InfoRequested,
            StatusPillPolicy.Approved,
            StatusPillPolicy.Rescheduled,
            StatusPillPolicy.Cancelled,
            StatusPillPolicy.NoShow,
            StatusPillPolicy.NotSeen,
            StatusPillPolicy.Rejected,
        });
    }
}
