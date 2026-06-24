using HealthcareSupport.CaseEvaluation.Enums;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.DoctorAvailabilities;

/// <summary>
/// Phase 7 (2026-05-03) -- pure unit tests for the four <c>internal static</c>
/// helpers extracted from <see cref="DoctorAvailabilitiesAppService"/>.
/// Mirrors the Phase 3 / 5 / 6 test pattern: bypass the ABP integration
/// harness (gated on the ABP Pro license blocker per
/// docs/handoffs/2026-05-03-test-host-license-blocker.md) and verify the
/// pure logic directly via <c>InternalsVisibleTo</c>.
///
/// Coverage:
///   1. <see cref="DoctorAvailabilitiesAppService.HasInFlightStatus"/> --
///      classifies a slot's <c>BookingStatus</c> as in-flight (Reserved /
///      Booked) or not. The Update / DeleteByDate guards depend on this.
///   2. <see cref="DoctorAvailabilitiesAppService.ComputeNumberOfSlotsPerDay"/>
///      -- mirrors OLD <c>DoctorsAvailabilityDomain.cs:310</c>. Trailing
///      partial slots are dropped (<c>Math.Floor</c>); zero / negative /
///      inverted inputs return 0 silently.
///   3. <see cref="DoctorAvailabilitiesAppService.IsValidSlotTimeRange"/>
///      -- FromTime &lt; ToTime, strict.
///   4. <see cref="DoctorAvailabilitiesAppService.IsValidSlotDateRange"/>
///      -- ToDate &gt;= FromDate.
/// </summary>
public class DoctorAvailabilitiesAppServiceUnitTests
{
    // ------------------------------------------------------------------
    // HasInFlightStatus
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(BookingStatus.Available, false)]
    [InlineData(BookingStatus.Reserved, true)]
    [InlineData(BookingStatus.Booked, true)]
    public void HasInFlightStatus_OnlyReservedAndBookedAreInFlight(BookingStatus status, bool expected)
    {
        DoctorAvailabilitiesAppService.HasInFlightStatus(status).ShouldBe(expected);
    }

    // ------------------------------------------------------------------
    // ComputeNumberOfSlotsPerDay
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(9, 0, 12, 0, 60, 3)]   // 09:00-12:00 with 60-min slots -> 3
    [InlineData(9, 0, 17, 0, 60, 8)]   // 09:00-17:00 with 60-min slots -> 8
    [InlineData(9, 0, 12, 30, 60, 3)]  // 09:00-12:30 with 60-min slots -> 3 (trailing 30 mins dropped)
    [InlineData(9, 0, 10, 0, 30, 2)]   // 09:00-10:00 with 30-min slots -> 2
    [InlineData(9, 0, 9, 30, 60, 0)]   // 09:00-09:30 with 60-min slots -> 0 (window smaller than duration)
    public void ComputeNumberOfSlotsPerDay_ReturnsFloorOfMinutesByDuration(
        int fromHour, int fromMin, int toHour, int toMin, int durationMinutes, int expected)
    {
        var result = DoctorAvailabilitiesAppService.ComputeNumberOfSlotsPerDay(
            new TimeOnly(fromHour, fromMin),
            new TimeOnly(toHour, toMin),
            durationMinutes);
        result.ShouldBe(expected);
    }

    [Fact]
    public void ComputeNumberOfSlotsPerDay_ZeroDuration_ReturnsZero()
    {
        DoctorAvailabilitiesAppService.ComputeNumberOfSlotsPerDay(
            new TimeOnly(9, 0), new TimeOnly(17, 0), durationMinutes: 0).ShouldBe(0);
    }

    [Fact]
    public void ComputeNumberOfSlotsPerDay_NegativeDuration_ReturnsZero()
    {
        DoctorAvailabilitiesAppService.ComputeNumberOfSlotsPerDay(
            new TimeOnly(9, 0), new TimeOnly(17, 0), durationMinutes: -30).ShouldBe(0);
    }

    [Fact]
    public void ComputeNumberOfSlotsPerDay_InvertedTimeRange_ReturnsZero()
    {
        DoctorAvailabilitiesAppService.ComputeNumberOfSlotsPerDay(
            new TimeOnly(17, 0), new TimeOnly(9, 0), durationMinutes: 60).ShouldBe(0);
    }

    [Fact]
    public void ComputeNumberOfSlotsPerDay_EmptyTimeRange_ReturnsZero()
    {
        DoctorAvailabilitiesAppService.ComputeNumberOfSlotsPerDay(
            new TimeOnly(9, 0), new TimeOnly(9, 0), durationMinutes: 60).ShouldBe(0);
    }

    // ------------------------------------------------------------------
    // IsValidSlotTimeRange
    // ------------------------------------------------------------------

    [Fact]
    public void IsValidSlotTimeRange_FromBeforeTo_True()
    {
        DoctorAvailabilitiesAppService
            .IsValidSlotTimeRange(new TimeOnly(9, 0), new TimeOnly(10, 0))
            .ShouldBeTrue();
    }

    [Fact]
    public void IsValidSlotTimeRange_FromEqualsTo_False()
    {
        DoctorAvailabilitiesAppService
            .IsValidSlotTimeRange(new TimeOnly(9, 0), new TimeOnly(9, 0))
            .ShouldBeFalse();
    }

    [Fact]
    public void IsValidSlotTimeRange_FromAfterTo_False()
    {
        DoctorAvailabilitiesAppService
            .IsValidSlotTimeRange(new TimeOnly(10, 0), new TimeOnly(9, 0))
            .ShouldBeFalse();
    }

    // ------------------------------------------------------------------
    // IsValidSlotDateRange
    // ------------------------------------------------------------------

    [Fact]
    public void IsValidSlotDateRange_ToOnSameDayAsFrom_True()
    {
        var d = new DateTime(2026, 6, 1);
        DoctorAvailabilitiesAppService.IsValidSlotDateRange(d, d).ShouldBeTrue();
    }

    [Fact]
    public void IsValidSlotDateRange_ToAfterFrom_True()
    {
        DoctorAvailabilitiesAppService.IsValidSlotDateRange(
            new DateTime(2026, 6, 1), new DateTime(2026, 6, 2)).ShouldBeTrue();
    }

    [Fact]
    public void IsValidSlotDateRange_ToBeforeFrom_False()
    {
        DoctorAvailabilitiesAppService.IsValidSlotDateRange(
            new DateTime(2026, 6, 2), new DateTime(2026, 6, 1)).ShouldBeFalse();
    }

    [Fact]
    public void IsValidSlotDateRange_TimeComponentIgnored()
    {
        // toDate at 00:00:01 on the same calendar day as fromDate at 23:59:59
        // should still be valid -- the helper compares Date components.
        DoctorAvailabilitiesAppService.IsValidSlotDateRange(
            new DateTime(2026, 6, 1, 23, 59, 59),
            new DateTime(2026, 6, 1, 0, 0, 1)).ShouldBeTrue();
    }
}
