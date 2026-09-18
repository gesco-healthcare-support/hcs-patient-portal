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
/// Pins <c>DashboardAppService.GetAsync</c> -- the legacy nav-badge counters.
///
/// <para>The seam is the SCOPE BRANCH plus the four live counters behind it. The
/// host branch is a per-office SUM (database-per-office means no single connection
/// sees every office) with the registry count taken ONCE; the tenant branch is the
/// same query set under ABP's tenant filter, with the two host-only tiles zeroed.
/// Three of the counters use three DIFFERENT date rules against two different
/// columns, so the tests below separate them with fixtures that no single rule
/// explains.</para>
///
/// <para>Every count assertion is a delta around the inserts this test made. The
/// rig accumulates, so an absolute count would either be asserting on rows the test
/// did not create or would break the first time another test inserted one.</para>
///
/// <para>NOT pinned here: <c>RejectedThisWeek</c>. It filters on
/// <c>LastModificationTime</c>, which ABP stamps only on an UPDATE and which cannot
/// be backdated through an insert, so the only reachable fixture is "modified now"
/// -- a row that lands inside the window whatever the boundary is. The eight
/// placeholder tiles that return a literal 0 are not pinned either; there is no
/// input that distinguishes them from a deleted assignment.</para>
/// </summary>
public abstract class DashboardCountersTests<TStartupModule> : DashboardTestsBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    /// <summary>
    /// The two host-only tiles are suppressed for a tenant caller.
    ///
    /// <para>The host call is the contrast case and is load-bearing: at zero, "the
    /// tenant view reports 0" would also pass with the whole doctor and office
    /// count deleted. Asserting the host reports them non-zero first means the
    /// zeroes are a suppression of something that exists.</para>
    ///
    /// <para>Honest label: both values are guarded TWICE in the source (the local
    /// ternary, and again in the DTO initialiser), so a single-site mutation
    /// survives this test. What it pins is the contract the nav badge binds to --
    /// a tenant caller never sees a cross-office total -- not the internals.</para>
    /// </summary>
    [Fact]
    public async Task GetAsync_InTenantScope_ZeroesTheHostOnlyDoctorAndOfficeTiles()
    {
        var host = await GetCountersAsync(null);
        host.TotalDoctors.ShouldBeGreaterThan(0);
        host.TotalTenants.ShouldBeGreaterThan(0);

        var tenant = await GetCountersAsync(TenantsTestData.TenantARef);

        tenant.TotalDoctors.ShouldBe(0);
        tenant.TotalTenants.ShouldBe(0);
    }

    /// <summary>
    /// Host counters are the SUM across offices, but the office count is taken once
    /// and explicitly not summed.
    ///
    /// <para>Both offices get a different, non-zero number of new Pending requests.
    /// Equal numbers would let "drop one office from the aggregate" survive, and a
    /// zero on either side would let "host == office A" survive.</para>
    /// </summary>
    [Fact]
    public async Task GetAsync_InHostScope_SumsEveryOfficeButCountsTheOfficesOnce()
    {
        await InsertAppointmentAsync(TenantsTestData.TenantARef, "C2a", AppointmentStatusType.Pending);
        await InsertAppointmentAsync(TenantsTestData.TenantBRef, "C2b", AppointmentStatusType.Pending);
        await InsertAppointmentAsync(TenantsTestData.TenantBRef, "C2c", AppointmentStatusType.Pending);

        var officeA = await GetCountersAsync(TenantsTestData.TenantARef);
        var officeB = await GetCountersAsync(TenantsTestData.TenantBRef);
        var host = await GetCountersAsync(null);
        var officeCount = await CountOfficesAsync();

        // Both terms non-zero, so neither "the host total is office A" nor "the host
        // total is office B" can survive the sum assertion below.
        officeA.PendingRequests.ShouldBeGreaterThan(0);
        officeB.PendingRequests.ShouldBeGreaterThan(0);
        host.PendingRequests.ShouldBe(officeA.PendingRequests + officeB.PendingRequests);
        host.PendingRequests.ShouldBeGreaterThan(officeA.PendingRequests);
        host.PendingRequests.ShouldBeGreaterThan(officeB.PendingRequests);

        // TotalTenants is the registry count, NOT the per-office value summed --
        // the per-office value is identical in every office, so summing it would
        // square the office count.
        officeCount.ShouldBeGreaterThanOrEqualTo(2);
        host.TotalTenants.ShouldBe(officeCount);
        host.TotalTenants.ShouldNotBe(officeCount * officeCount);
    }

    /// <summary>
    /// The decision-overdue tile uses the per-office decision window; the legal
    /// tile uses a fixed 60-day threshold. Both are Pending-only.
    ///
    /// <para>The fixture is chosen so that neither rule explains the other's result:
    /// a Pending request four days old is past the 3-day decision window but
    /// nowhere near 60 days, and the 61-day-old row is Approved, so it is the row
    /// that proves the legal tile's status filter is doing work.</para>
    ///
    /// <para>Boundaries are built from Pacific midday rather than
    /// <c>UtcNow.AddDays(-4)</c> so a daylight-saving transition inside the window
    /// cannot move a row across the Pacific-date boundary the cutoff is derived
    /// from.</para>
    /// </summary>
    [Fact]
    public async Task GetAsync_DecisionOverdueUsesTheOfficeWindowNotTheSixtyDayLegalThreshold()
    {
        var nowUtc = DateTime.UtcNow;
        var pacificToday = PacificTime.TodayFrom(nowUtc);
        var decisionDueDays = await GetDecisionDueDaysAsync(TenantsTestData.TenantARef);
        decisionDueDays.ShouldBe(3, "The fixture offsets below assume the seeded 3-day window.");

        var before = await GetCountersAsync(TenantsTestData.TenantARef);

        // Past the decision window (4 days old), far short of the legal threshold.
        await InsertAppointmentAsync(
            TenantsTestData.TenantARef,
            "C3old",
            AppointmentStatusType.Pending,
            createdAtUtc: PacificTime.StartOfDayUtc(pacificToday.AddDays(-4)).AddHours(12));

        // Inside the decision window.
        await InsertAppointmentAsync(
            TenantsTestData.TenantARef,
            "C3new",
            AppointmentStatusType.Pending,
            createdAtUtc: nowUtc);

        // Old enough for the legal threshold, but decided -- so neither tile counts
        // it. This is the row that makes the Pending filter on the legal tile
        // provable; without it the tile reads 0 either way.
        await InsertAppointmentAsync(
            TenantsTestData.TenantARef,
            "C3done",
            AppointmentStatusType.Approved,
            createdAtUtc: nowUtc.AddDays(-61));

        var after = await GetCountersAsync(TenantsTestData.TenantARef);

        (after.DecisionOverdue - before.DecisionOverdue).ShouldBe(1);
        (after.RequestsApproachingLegalDeadline - before.RequestsApproachingLegalDeadline).ShouldBe(0);
    }

    /// <summary>
    /// "Approved this week" needs the Approved status AND a recent approval date.
    ///
    /// <para>Three contrast rows, each defeating a different single-clause deletion:
    /// an old approval (defeats dropping the week boundary), a REJECTED row carrying
    /// a recent approval date (defeats dropping the status filter -- a Pending row
    /// would not do, because its approval date is null and the null check would
    /// still exclude it), and the approved-now row that should be the only hit.</para>
    ///
    /// <para>Honest label: fourteen days is unambiguously before any Monday, so this
    /// pins "a recent window excludes an old approval". It does NOT pin the exact
    /// Monday boundary, which would require re-deriving the service's own week
    /// arithmetic inside the test.</para>
    /// </summary>
    [Fact]
    public async Task GetAsync_ApprovedThisWeekCountsOnlyRecentApprovalsOfApprovedRequests()
    {
        var nowUtc = DateTime.UtcNow;
        var before = await GetCountersAsync(TenantsTestData.TenantARef);

        await InsertAppointmentAsync(
            TenantsTestData.TenantARef,
            "C4hit",
            AppointmentStatusType.Approved,
            approveDateUtc: nowUtc);

        await InsertAppointmentAsync(
            TenantsTestData.TenantARef,
            "C4old",
            AppointmentStatusType.Approved,
            approveDateUtc: nowUtc.AddDays(-14));

        // Physically impossible in production (a rejected request holding an
        // approval date) and deliberately so: it exists only to separate the status
        // clause from the date clause.
        await InsertAppointmentAsync(
            TenantsTestData.TenantARef,
            "C4rej",
            AppointmentStatusType.Rejected,
            approveDateUtc: nowUtc);

        var after = await GetCountersAsync(TenantsTestData.TenantARef);

        (after.ApprovedThisWeek - before.ApprovedThisWeek).ShouldBe(1);
    }

    /// <summary>
    /// The pending change-request tile is scoped like the appointment counts:
    /// tenant-filtered for an office caller, summed for the host.
    ///
    /// <para>The decided request is the negative the status filter needs; the
    /// office-B request is the negative the tenant filter needs. With neither
    /// seeded, both filters could be deleted and the count would be unchanged.</para>
    /// </summary>
    [Fact]
    public async Task GetAsync_PendingChangeRequestsAreStatusFilteredAndOfficeScoped()
    {
        var beforeA = await GetCountersAsync(TenantsTestData.TenantARef);

        var pendingRow = await InsertAppointmentAsync(
            TenantsTestData.TenantARef, "C5a", AppointmentStatusType.Approved);
        var decidedRow = await InsertAppointmentAsync(
            TenantsTestData.TenantARef, "C5d", AppointmentStatusType.Approved);
        var otherOfficeRow = await InsertAppointmentAsync(
            TenantsTestData.TenantBRef, "C5b", AppointmentStatusType.Approved);

        await InsertChangeRequestAsync(TenantsTestData.TenantARef, pendingRow.Id, RequestStatusType.Pending);
        await InsertChangeRequestAsync(TenantsTestData.TenantARef, decidedRow.Id, RequestStatusType.Accepted);
        await InsertChangeRequestAsync(TenantsTestData.TenantBRef, otherOfficeRow.Id, RequestStatusType.Pending);

        var afterA = await GetCountersAsync(TenantsTestData.TenantARef);

        (afterA.PendingChangeRequests - beforeA.PendingChangeRequests).ShouldBe(1);
    }
}
