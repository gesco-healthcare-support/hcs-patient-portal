using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp.Modularity;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Dashboards;

/// <summary>
/// Pins the two range-windowed chart sections of the tenant dashboard: the status
/// donut and the weekly trend.
///
/// <para>The donut's contract is that it always returns EVERY pill, in the
/// prototype's order, including the empty ones -- the UI binds to the shape, not to
/// whatever statuses happen to exist -- and that statuses with no pill (the
/// in-flight "Requested" states and the legacy day-of-exam states) are dropped
/// rather than bucketed somewhere. The trend's contract is two different date
/// columns in one row: volume counts requests RECEIVED by creation date,
/// completion counts requests APPROVED by approval date.</para>
///
/// <para>These tests read <c>StatusPillPolicy</c>, which is internal to the
/// Application assembly. That is why this family is shaped as an abstract body here
/// with a runner in EntityFrameworkCore.Tests rather than living entirely next to
/// the other EF-backed service tests.</para>
///
/// <para>NOT pinned here: the seven-day BUCKET WIDTH on its own. The service takes
/// its "now" from <c>DateTime.UtcNow</c> with no clock seam, so how many buckets a
/// range produces depends on today's calendar date, and an assertion on the count
/// would pass or fail by the day rather than by the code. The Month test below
/// pins bucket SPACING instead, which is exercised on every day of a month past the
/// seventh and is vacuous before it -- said plainly rather than implied.</para>
/// </summary>
public abstract class DashboardTrendAndDonutTests<TStartupModule> : DashboardTestsBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private static int SliceCount(DashboardDto dto, string pill) =>
        dto.StatusBreakdown.Single(s => s.Pill == pill).Count;

    private static int AllSlicesCount(DashboardDto dto) =>
        dto.StatusBreakdown.Sum(s => s.Count);

    /// <summary>
    /// The donut always carries the full pill set, in order, empty slices included.
    ///
    /// <para>Shape-only by design: this is the contract the chart binds to. It fails
    /// the moment the projection is built from the grouped rows or from the
    /// dictionary's keys instead of from the declared order.</para>
    /// </summary>
    [Fact]
    public async Task GetDashboardAsync_StatusBreakdownAlwaysReturnsEveryPillInDonutOrder()
    {
        var dto = await GetDashboardAsync(TenantsTestData.TenantARef, DashboardRange.Week);

        dto.StatusBreakdown.Select(s => s.Pill).ShouldBe(StatusPillPolicy.DonutOrder);
    }

    /// <summary>
    /// The donut buckets by pill and DROPS statuses that have no pill.
    ///
    /// <para>The two unpilled rows are the load-bearing part of this fixture, not
    /// setup noise: the guard that skips them can only be proven by a row it has to
    /// skip. Four rows go in, the slices move by two.</para>
    /// </summary>
    [Fact]
    public async Task GetDashboardAsync_StatusBreakdownBucketsByPillAndDropsStatusesWithNoPill()
    {
        var before = await GetDashboardAsync(TenantsTestData.TenantARef, DashboardRange.Week);

        await InsertAppointmentAsync(TenantsTestData.TenantARef, "D2p", AppointmentStatusType.Pending);
        await InsertAppointmentAsync(TenantsTestData.TenantARef, "D2c", AppointmentStatusType.CancelledLate);
        // No donut pill: a day-of-exam state and an in-flight change-request state.
        await InsertAppointmentAsync(TenantsTestData.TenantARef, "D2i", AppointmentStatusType.CheckedIn);
        await InsertAppointmentAsync(TenantsTestData.TenantARef, "D2r", AppointmentStatusType.RescheduleRequested);

        var after = await GetDashboardAsync(TenantsTestData.TenantARef, DashboardRange.Week);

        (SliceCount(after, StatusPillPolicy.Pending) - SliceCount(before, StatusPillPolicy.Pending))
            .ShouldBe(1);
        (SliceCount(after, StatusPillPolicy.Cancelled) - SliceCount(before, StatusPillPolicy.Cancelled))
            .ShouldBe(1);
        (AllSlicesCount(after) - AllSlicesCount(before)).ShouldBe(2);
    }

    /// <summary>
    /// The donut is windowed to the selected range: a request created before the
    /// window start contributes to no slice.
    /// </summary>
    [Fact]
    public async Task GetDashboardAsync_StatusBreakdownIgnoresRequestsCreatedBeforeTheWindow()
    {
        var before = await GetDashboardAsync(TenantsTestData.TenantARef, DashboardRange.Week);

        await InsertAppointmentAsync(
            TenantsTestData.TenantARef,
            "D3old",
            AppointmentStatusType.Pending,
            createdAtUtc: DateTime.UtcNow.AddDays(-90));

        var after = await GetDashboardAsync(TenantsTestData.TenantARef, DashboardRange.Week);

        (AllSlicesCount(after) - AllSlicesCount(before)).ShouldBe(0);
    }

    /// <summary>
    /// The Week range is exactly one bucket, labelled from one, starting at the
    /// week boundary.
    ///
    /// <para>The week window is shorter than one bucket on every day of the week, so
    /// the single-bucket result is date-independent. The start is asserted as a
    /// Monday inside the last seven days rather than against a re-derived constant,
    /// which is what makes it an independent check of the window rather than a
    /// restatement of the service's arithmetic.</para>
    /// </summary>
    [Fact]
    public async Task GetDashboardAsync_WeekRangeProducesOneBucketStartingAtTheWeekBoundary()
    {
        var nowUtc = DateTime.UtcNow;

        var dto = await GetDashboardAsync(TenantsTestData.TenantARef, DashboardRange.Week);

        dto.Trend.Count.ShouldBe(1);
        dto.Trend[0].Label.ShouldBe("Wk 1");
        dto.Trend[0].WeekStart.DayOfWeek.ShouldBe(DayOfWeek.Monday);
        dto.Trend[0].WeekStart.ShouldBeLessThanOrEqualTo(nowUtc);
        dto.Trend[0].WeekStart.ShouldBeGreaterThan(nowUtc.AddDays(-7));
        dto.Trend[0].WeekStart.TimeOfDay.ShouldBe(TimeSpan.Zero);
    }

    /// <summary>
    /// The Month range starts the trend at the first of the month and spaces the
    /// buckets seven days apart, labelling them in order.
    ///
    /// <para>Honest label: the spacing loop below is exercised only once a month is
    /// more than seven days old. On the first through the seventh there is a single
    /// bucket and the loop body never runs, so on those days this test pins the
    /// window ORIGIN and the label only.</para>
    /// </summary>
    [Fact]
    public async Task GetDashboardAsync_MonthRangeStartsAtTheFirstOfTheMonthAndSpacesBucketsWeekly()
    {
        var nowUtc = DateTime.UtcNow;
        var monthStartUtc = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var dto = await GetDashboardAsync(TenantsTestData.TenantARef, DashboardRange.Month);

        dto.Trend.ShouldNotBeEmpty();
        dto.Trend[0].WeekStart.ShouldBe(monthStartUtc);
        dto.Trend[0].Label.ShouldBe("Wk 1");

        for (var index = 1; index < dto.Trend.Count; index++)
        {
            dto.Trend[index].WeekStart.ShouldBe(monthStartUtc.AddDays(7 * index));
            dto.Trend[index].Label.ShouldBe($"Wk {index + 1}");
        }
    }

    /// <summary>
    /// In one trend point, volume is keyed on creation date and completion on
    /// approval date.
    ///
    /// <para>The two deltas are deliberately unequal (three versus one). Equal
    /// counts would let either column read the other's date field and survive. Two
    /// of the rows are physically impossible in production -- approved three months
    /// before they were created -- and exist only to separate the two columns.</para>
    /// </summary>
    [Fact]
    public async Task GetDashboardAsync_TrendCountsVolumeByCreationDateAndCompletionByApprovalDate()
    {
        var nowUtc = DateTime.UtcNow;
        var before = await GetDashboardAsync(TenantsTestData.TenantARef, DashboardRange.Week);
        before.Trend.Count.ShouldBe(1);

        // Received this week, not approved this week.
        await InsertAppointmentAsync(TenantsTestData.TenantARef, "D6p", AppointmentStatusType.Pending);
        await InsertAppointmentAsync(
            TenantsTestData.TenantARef,
            "D6a1",
            AppointmentStatusType.Approved,
            approveDateUtc: nowUtc.AddDays(-90));
        await InsertAppointmentAsync(
            TenantsTestData.TenantARef,
            "D6a2",
            AppointmentStatusType.Approved,
            approveDateUtc: nowUtc.AddDays(-90));

        // Approved this week, not received this week.
        await InsertAppointmentAsync(
            TenantsTestData.TenantARef,
            "D6old",
            AppointmentStatusType.Approved,
            createdAtUtc: nowUtc.AddDays(-90),
            approveDateUtc: nowUtc);

        var after = await GetDashboardAsync(TenantsTestData.TenantARef, DashboardRange.Week);
        after.Trend.Count.ShouldBe(1);

        (after.Trend[0].Count - before.Trend[0].Count).ShouldBe(3);
        (after.Trend[0].CompletedCount - before.Trend[0].CompletedCount).ShouldBe(1);
    }
}
