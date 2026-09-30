using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.TestData;
using HealthcareSupport.CaseEvaluation.Timing;
using Shouldly;
using Volo.Abp.Modularity;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Dashboards;

/// <summary>
/// Pins the tenant branch of <c>DashboardAppService.GetDashboardAsync</c>: the hero
/// KPIs and the decision-deadline section.
///
/// <para>Two rules are worth the ceremony here. First, live Pending tiles are
/// SNAPSHOTS -- previous equals current, so the UI shows no delta badge -- while
/// Approved and Rejected are period-windowed with a prior-period comparison.
/// Second, the decision-deadline band is narrower than it looks: with the seeded
/// 3-day window and the 2-day approach window the band is [today - 3, today) in
/// PACIFIC dates, so a request created TODAY is excluded (it is not due yet) and
/// one created four days ago is excluded too (it is already overdue, not
/// approaching).</para>
///
/// <para>NOT pinned here: the Rejected KPI's window. It filters on
/// <c>LastModificationTime</c>, which cannot be backdated through an insert, so no
/// fixture distinguishes the current window from the previous one.</para>
/// </summary>
public abstract class DashboardTenantKpiTests<TStartupModule> : DashboardTestsBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    /// <summary>
    /// The payload is marked as a tenant view and the two live Pending tiles carry
    /// no period delta.
    ///
    /// <para>The fixture drives both tiles above zero first. At zero,
    /// "previous equals current" is vacuously true and would still pass with the
    /// snapshot helper returning a hardcoded previous value of 0.</para>
    /// </summary>
    [Fact]
    public async Task GetDashboardAsync_InTenantScope_MarksThePayloadTenantAndSnapshotsThePendingTiles()
    {
        var approved = await InsertAppointmentAsync(
            TenantsTestData.TenantARef, "K1a", AppointmentStatusType.Approved);
        await InsertAppointmentAsync(
            TenantsTestData.TenantARef, "K1p", AppointmentStatusType.Pending);
        await InsertChangeRequestAsync(
            TenantsTestData.TenantARef, approved.Id, RequestStatusType.Pending);

        var dto = await GetDashboardAsync(TenantsTestData.TenantARef, DashboardRange.Week);

        dto.IsHost.ShouldBeFalse();

        dto.PendingRequests.Value.ShouldBeGreaterThan(0);
        dto.PendingRequests.PreviousValue.ShouldBe(dto.PendingRequests.Value);

        dto.PendingChangeRequests.Value.ShouldBeGreaterThan(0);
        dto.PendingChangeRequests.PreviousValue.ShouldBe(dto.PendingChangeRequests.Value);
    }

    /// <summary>
    /// The pending change-request KPI counts undecided requests in THIS office only.
    ///
    /// <para>A negative guarantee needs the thing being excluded to exist, so the
    /// fixture seeds both exclusions: a decided request in the same office and a
    /// pending one in the other office. Either filter deleted moves the delta to 2.</para>
    /// </summary>
    [Fact]
    public async Task GetDashboardAsync_PendingChangeRequestKpiCountsOnlyUndecidedRequestsInThisOffice()
    {
        var before = await GetDashboardAsync(TenantsTestData.TenantARef, DashboardRange.Week);

        var pendingRow = await InsertAppointmentAsync(
            TenantsTestData.TenantARef, "K2p", AppointmentStatusType.Approved);
        var decidedRow = await InsertAppointmentAsync(
            TenantsTestData.TenantARef, "K2d", AppointmentStatusType.Approved);
        var otherOfficeRow = await InsertAppointmentAsync(
            TenantsTestData.TenantBRef, "K2o", AppointmentStatusType.Approved);

        await InsertChangeRequestAsync(TenantsTestData.TenantARef, pendingRow.Id, RequestStatusType.Pending);
        await InsertChangeRequestAsync(TenantsTestData.TenantARef, decidedRow.Id, RequestStatusType.Rejected);
        await InsertChangeRequestAsync(TenantsTestData.TenantBRef, otherOfficeRow.Id, RequestStatusType.Pending);

        var after = await GetDashboardAsync(TenantsTestData.TenantARef, DashboardRange.Week);

        (after.PendingChangeRequests.Value - before.PendingChangeRequests.Value).ShouldBe(1);
    }

    /// <summary>
    /// The deadline list holds requests APPROACHING the decision deadline, and
    /// excludes both ends: already-overdue requests below the band, and requests
    /// created today above it.
    ///
    /// <para>All three rows are Pending and differ only in age, so nothing but the
    /// band explains the result. The day arithmetic is Pacific on both sides: the
    /// due date is a Pacific date and <c>DaysRemaining</c> is measured against
    /// Pacific today, which is the fix for a real bug -- taking <c>.Date</c> off the
    /// raw UTC instant reported one day fewer for the last seven hours of every
    /// Pacific day.</para>
    ///
    /// <para>Fixture boundaries are built from Pacific midnight plus twelve hours
    /// rather than from <c>UtcNow.AddDays(-n)</c> so a daylight-saving transition
    /// inside the window cannot shift a row onto the wrong Pacific date.</para>
    /// </summary>
    [Fact]
    public async Task GetDashboardAsync_DeadlineListHoldsTheApproachBandAndExcludesBothEnds()
    {
        var nowUtc = DateTime.UtcNow;
        var pacificToday = PacificTime.TodayFrom(nowUtc);
        var decisionDueDays = await GetDecisionDueDaysAsync(TenantsTestData.TenantARef);
        decisionDueDays.ShouldBe(3, "The expected band and DaysRemaining below assume the seeded 3-day window.");

        var before = await GetDashboardAsync(TenantsTestData.TenantARef, DashboardRange.Week);

        var inBand = await InsertAppointmentAsync(
            TenantsTestData.TenantARef,
            "K3in",
            AppointmentStatusType.Pending,
            createdAtUtc: PacificTime.StartOfDayUtc(pacificToday.AddDays(-1)).AddHours(12));

        var alreadyOverdue = await InsertAppointmentAsync(
            TenantsTestData.TenantARef,
            "K3ovr",
            AppointmentStatusType.Pending,
            createdAtUtc: PacificTime.StartOfDayUtc(pacificToday.AddDays(-4)).AddHours(12));

        var createdToday = await InsertAppointmentAsync(
            TenantsTestData.TenantARef,
            "K3new",
            AppointmentStatusType.Pending,
            createdAtUtc: nowUtc);

        var after = await GetDashboardAsync(TenantsTestData.TenantARef, DashboardRange.Week);

        (after.DeadlineApproachingCount - before.DeadlineApproachingCount).ShouldBe(1);

        after.Deadlines.ShouldContain(d => d.ConfirmationNumber == inBand.ConfirmationNumber);
        after.Deadlines.ShouldNotContain(d => d.ConfirmationNumber == alreadyOverdue.ConfirmationNumber);
        after.Deadlines.ShouldNotContain(d => d.ConfirmationNumber == createdToday.ConfirmationNumber);

        var item = after.Deadlines.First(d => d.ConfirmationNumber == inBand.ConfirmationNumber);
        item.AppointmentId.ShouldBe(inBand.Id);
        item.DueDate.ShouldBe(pacificToday.AddDays(2));
        item.DaysRemaining.ShouldBe(2);

        // The patient join is what puts a name on the staff-facing row; the
        // assertion also pins the first-then-last ordering of the two columns.
        item.PatientName.ShouldBe(
            $"{PatientsTestData.Patient1FirstName} {PatientsTestData.Patient1LastName}");
    }

    /// <summary>
    /// The Month range splits approvals across the current and the prior month.
    ///
    /// <para>Two approvals an hour apart in calendar terms but on opposite sides of
    /// the month boundary. Collapsing the previous window onto the current one
    /// empties the prior bucket; dropping its upper bound doubles it.</para>
    ///
    /// <para>Honest label: the test recomputes the month boundary the same way the
    /// service does, so it pins the SHAPE of the two windows, not an independently
    /// derived boundary. Note also that on the first of a month the current-window
    /// fixture instant can sit slightly in the future -- harmless, because the
    /// current window has no upper bound by design.</para>
    /// </summary>
    [Fact]
    public async Task GetDashboardAsync_MonthRangeSplitsApprovalsAcrossTheCurrentAndPriorMonth()
    {
        var nowUtc = DateTime.UtcNow;
        var monthStartUtc = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var before = await GetDashboardAsync(TenantsTestData.TenantARef, DashboardRange.Month);

        await InsertAppointmentAsync(
            TenantsTestData.TenantARef,
            "K4cur",
            AppointmentStatusType.Approved,
            approveDateUtc: monthStartUtc.AddHours(1));

        await InsertAppointmentAsync(
            TenantsTestData.TenantARef,
            "K4prv",
            AppointmentStatusType.Approved,
            approveDateUtc: monthStartUtc.AddDays(-1));

        var after = await GetDashboardAsync(TenantsTestData.TenantARef, DashboardRange.Month);

        (after.ApprovedRequests.Value - before.ApprovedRequests.Value).ShouldBe(1);
        (after.ApprovedRequests.PreviousValue - before.ApprovedRequests.PreviousValue).ShouldBe(1);
    }

    /// <summary>
    /// The Week range's Approved KPI counts by APPROVAL date, not creation date.
    ///
    /// <para>The fixture is an approval that could not have been created this week:
    /// an Approved request created ninety days ago and approved just now. If the
    /// KPI keyed on creation time it would miss the row entirely.</para>
    /// </summary>
    [Fact]
    public async Task GetDashboardAsync_ApprovedKpiCountsByApprovalDateNotCreationDate()
    {
        var nowUtc = DateTime.UtcNow;
        var before = await GetDashboardAsync(TenantsTestData.TenantARef, DashboardRange.Week);

        await InsertAppointmentAsync(
            TenantsTestData.TenantARef,
            "K5old",
            AppointmentStatusType.Approved,
            createdAtUtc: nowUtc.AddDays(-90),
            approveDateUtc: nowUtc);

        var after = await GetDashboardAsync(TenantsTestData.TenantARef, DashboardRange.Week);

        (after.ApprovedRequests.Value - before.ApprovedRequests.Value).ShouldBe(1);
    }
}
