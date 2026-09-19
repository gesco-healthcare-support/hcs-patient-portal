using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.TestData;
using HealthcareSupport.CaseEvaluation.Timing;
using Shouldly;
using Volo.Abp.Modularity;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Dashboards;

/// <summary>
/// Pins the two list sections of the tenant dashboard: the recent-activity feed and
/// today's schedule.
///
/// <para>Today's schedule is the section most likely to look covered while proving
/// nothing. It inner-joins the appointment-type and location catalogs, and BOTH of
/// those are per-office entities that the integration seed inserts host-scoped --
/// so from inside an office the seeded catalog is invisible and the join returns
/// nothing whatever appointments exist. A schedule test built on the seeded catalog
/// would pass with the entire method deleted. The fixture below therefore creates a
/// catalog row owned by the office.</para>
///
/// <para>The schedule window is also the opposite treatment to the deadline band ten
/// lines away in the same service: <c>AppointmentDate</c> is a CALENDAR DATE column,
/// so the Pacific day boundary must NOT be converted to a UTC instant. The fixture
/// uses an early-morning appointment precisely because converting would move the
/// window forward seven hours and drop it.</para>
///
/// <para>NOT pinned here, and deliberately: the eight-row cap on the schedule
/// (filling it would need nine same-day appointments whose ordering the test then
/// has to own, and the cap is a display limit rather than a rule about which rows
/// qualify), and the activity feed's RANGE WINDOW. The feed is sorted newest-first
/// and capped at six, so a row old enough to fall outside the window is also a row
/// that any six newer rows would push off the list anyway -- "it is absent" would
/// pass with the window filter deleted, which is worse than no test. The equivalent
/// windowing rule IS pinned on the status donut, which has no such cap.</para>
/// </summary>
public abstract class DashboardActivityAndScheduleTests<TStartupModule> : DashboardTestsBase<TStartupModule>
    where TStartupModule : IAbpModule
{

    /// <summary>
    /// Today's schedule holds today's PACIFIC calendar date only, with the type and
    /// location names resolved through the office's own catalog.
    ///
    /// <para>The two fixture appointments differ only by one day, so the upper bound
    /// is the only thing that separates them. The 02:15 time is deliberate: it is
    /// the hour at which converting the Pacific day boundary into a UTC instant --
    /// the plausible-looking wrong fix, and the right one for the deadline band in
    /// the same file -- would push the window past the row and drop it.</para>
    ///
    /// <para>Side effect worth knowing: the catalog rows created here belong to
    /// office A, so they are invisible to the host-scoped location and
    /// appointment-type tests, which match only rows with no office.</para>
    /// </summary>
    [Fact]
    public async Task GetDashboardAsync_TodayScheduleHoldsTodaysPacificDateWithCatalogNames()
    {
        var pacificToday = PacificTime.TodayFrom(DateTime.UtcNow);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var typeName = $"TEST-DSH-Type-{suffix}";
        var locationName = $"TEST-DSH-Loc-{suffix}";

        var typeId = await InsertOfficeAppointmentTypeAsync(TenantsTestData.TenantARef, typeName);
        var locationId = await InsertOfficeLocationAsync(TenantsTestData.TenantARef, locationName);

        var todayAt = pacificToday.AddHours(2).AddMinutes(15);
        var tomorrowAt = pacificToday.AddDays(1).AddHours(2).AddMinutes(15);

        await InsertAppointmentAsync(
            TenantsTestData.TenantARef,
            "A3day",
            AppointmentStatusType.Approved,
            appointmentDate: todayAt,
            appointmentTypeId: typeId,
            locationId: locationId);

        await InsertAppointmentAsync(
            TenantsTestData.TenantARef,
            "A3tmw",
            AppointmentStatusType.Approved,
            appointmentDate: tomorrowAt,
            appointmentTypeId: typeId,
            locationId: locationId);

        var dto = await GetDashboardAsync(TenantsTestData.TenantARef, DashboardRange.Week);

        dto.TodaySchedule.ShouldContain(s =>
            s.AppointmentDate == todayAt
            && s.AppointmentType == typeName
            && s.Location == locationName);

        dto.TodaySchedule.ShouldNotContain(s => s.AppointmentDate == tomorrowAt);
    }
}
