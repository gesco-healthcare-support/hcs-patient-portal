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
    public void ToPill_MapsActiveStatusesToTheirPill(AppointmentStatusType status, string expectedPill)
    {
        StatusPillPolicy.ToPill(status).ShouldBe(expectedPill);
    }

    [Theory]
    [InlineData(AppointmentStatusType.NoShow)]
    [InlineData(AppointmentStatusType.CheckedIn)]
    [InlineData(AppointmentStatusType.CheckedOut)]
    [InlineData(AppointmentStatusType.Billed)]
    [InlineData(AppointmentStatusType.RescheduleRequested)]
    [InlineData(AppointmentStatusType.CancellationRequested)]
    public void ToPill_ReturnsNullForStatusesWithoutADonutPill(AppointmentStatusType status)
    {
        StatusPillPolicy.ToPill(status).ShouldBeNull();
    }

    [Fact]
    public void DonutOrder_IsTheSixPillsInPrototypeOrder()
    {
        StatusPillPolicy.DonutOrder.ShouldBe(new[]
        {
            StatusPillPolicy.Pending,
            StatusPillPolicy.InfoRequested,
            StatusPillPolicy.Approved,
            StatusPillPolicy.Rescheduled,
            StatusPillPolicy.Cancelled,
            StatusPillPolicy.Rejected,
        });
    }
}
