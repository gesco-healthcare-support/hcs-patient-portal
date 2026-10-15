using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.TestData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shouldly;
using Volo.Abp.Modularity;
using Volo.Abp.Timing;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Dashboards;

/// <summary>
/// The Month trend's buckets, with the service's clock PINNED -- the test the dashboard could not
/// support until it read "now" through <see cref="IClock"/>.
///
/// <para>Before the seam, the service read <c>DateTime.UtcNow</c> itself, so a trend test could only
/// assert whatever the real calendar allowed that day. On 2026-10-01 a Month assertion that holds
/// only from the 8th failed every open pull request (#1194). Its fix derived the expectation from
/// today, which stopped the outage but pinned nothing on the 1st to the 7th: widening the buckets
/// from seven days to six left it green, because one bucket only proves where it starts.</para>
///
/// <para>Here the date is chosen, so every risky window is tested on purpose, every run: the 1st,
/// the 7th (the last day with one bucket), the 8th (the first with two), the end of a 31-day month
/// and the end of February. Each case places one row six and a half days into every expected bucket
/// and a sentinel just past the last one. A seven-day bucket counts exactly its own row; a narrower
/// one loses it and a wider one swallows the next row or the sentinel -- so the width is pinned
/// from both sides even when there is only one bucket.</para>
///
/// <para>March 2031 is used because no seeded row can fall in it: the integration seed stamps
/// <c>CreationTime</c> from this same clock, which reads 2020 until a test pins it.</para>
/// </summary>
public abstract class DashboardTrendPinnedClockTests<TStartupModule> : DashboardTestsBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    // Field initializers run before the base constructor, so this exists when ABP calls
    // AfterAddApplication during that constructor.
    private readonly PinnedClock _clock = new();

    protected override void AfterAddApplication(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Singleton<IClock>(_clock));
    }

    [Theory]
    [InlineData(2031, 3, 1, 1)]   // the 1st: one bucket, and it is today
    [InlineData(2031, 3, 7, 1)]   // the 7th: still one bucket -- the window #1194 could not pin
    [InlineData(2031, 3, 8, 2)]   // the 8th: the second bucket starts
    [InlineData(2031, 3, 31, 5)]  // end of a 31-day month
    [InlineData(2031, 2, 28, 4)]  // end of February: exactly four weeks
    public async Task GetDashboardAsync_MonthTrend_SpacesSevenDayBucketsFromTheFirst(
        int year, int month, int day, int expectedBuckets)
    {
        var monthStart = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        _clock.Now = new DateTime(year, month, day, 12, 0, 0, DateTimeKind.Utc);

        for (var bucket = 0; bucket < expectedBuckets; bucket++)
        {
            await InsertAppointmentAsync(
                TenantsTestData.TenantARef,
                $"TP{day}b{bucket}",
                AppointmentStatusType.Pending,
                createdAtUtc: monthStart.AddDays((7 * bucket) + 6.5));
        }
        // Just past the end of the last bucket: no seven-day bucket may count it.
        await InsertAppointmentAsync(
            TenantsTestData.TenantARef,
            $"TP{day}past",
            AppointmentStatusType.Pending,
            createdAtUtc: monthStart.AddDays((7 * expectedBuckets) + 0.25));

        var dto = await GetDashboardAsync(TenantsTestData.TenantARef, DashboardRange.Month);

        dto.Trend.Count.ShouldBe(expectedBuckets,
            $"on {_clock.Now:yyyy-MM-dd} the month range must produce one bucket per seven-day step from the first");
        for (var bucket = 0; bucket < expectedBuckets; bucket++)
        {
            dto.Trend[bucket].WeekStart.ShouldBe(monthStart.AddDays(7 * bucket));
            dto.Trend[bucket].Label.ShouldBe($"Wk {bucket + 1}");
            dto.Trend[bucket].Count.ShouldBe(1,
                $"bucket {bucket + 1} must count exactly the row placed 6.5 days into it -- a narrower "
                + "bucket loses it and a wider one also takes the next row");
        }
    }

    [Theory]
    [InlineData(2031, 3, 5, 3)]   // a Wednesday: back to Monday the 3rd
    [InlineData(2031, 3, 9, 3)]   // a Sunday: the furthest back the week reaches
    [InlineData(2031, 3, 10, 10)] // a Monday: the week starts today
    public async Task GetDashboardAsync_WeekTrend_StartsOnTheMondayOfThePinnedWeek(
        int year, int month, int day, int expectedMondayDay)
    {
        // The Week window comes from the Monday helper, which used to read the clock itself instead
        // of the "now" its caller held. Pinned, its answer is exact on each edge of the week.
        _clock.Now = new DateTime(year, month, day, 12, 0, 0, DateTimeKind.Utc);

        var dto = await GetDashboardAsync(TenantsTestData.TenantARef, DashboardRange.Week);

        dto.Trend.Count.ShouldBe(1);
        dto.Trend[0].WeekStart.ShouldBe(new DateTime(year, month, expectedMondayDay, 0, 0, 0, DateTimeKind.Utc));
    }

    /// <summary>A clock a test can set. Kind is Utc, matching <c>AbpClockOptions.Kind</c>.</summary>
    private sealed class PinnedClock : IClock
    {
        public DateTime Now { get; set; } = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public DateTimeKind Kind => DateTimeKind.Utc;

        public bool SupportsMultipleTimezone => false;

        public DateTime Normalize(DateTime dateTime) =>
            dateTime.Kind == DateTimeKind.Utc ? dateTime : DateTime.SpecifyKind(dateTime.ToUniversalTime(), DateTimeKind.Utc);

        public DateTime ConvertToUserTime(DateTime utcDateTime) => utcDateTime;

        public DateTimeOffset ConvertToUserTime(DateTimeOffset dateTimeOffset) => dateTimeOffset;

        public DateTime ConvertToUtc(DateTime dateTime) => Normalize(dateTime);
    }
}
