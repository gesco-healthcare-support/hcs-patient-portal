using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Volo.Saas.Editions;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Dashboards;

/// <summary>
/// Dashboard rules the other dashboard classes leave unreached: the named sort fields of the
/// office list and the office breakdown, the activity feed's wording for the statuses no other
/// test puts in it, and the Quarter range window.
///
/// <para>Each sort test asserts the RELATIVE order of the two seeded offices after making their
/// values differ in a known direction, and each checks both directions, so a sort that ignored
/// its field (or its direction) fails one of the two halves.</para>
///
/// <para>The activity feed and the Quarter KPI are office-scoped, so each carries an OFFICE decoy:
/// the same kind of row in office B, which office A's dashboard must not show or count.</para>
/// </summary>
public abstract class DashboardSortingActivityAndQuarterTests<TStartupModule> : DashboardTestsBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IRepository<Edition, Guid> _editions;

    protected DashboardSortingActivityAndQuarterTests()
    {
        _editions = GetRequiredService<IRepository<Edition, Guid>>();
    }

    [Fact]
    public async Task GetOfficesAsync_SortsByEditionName_BlankEditionFirstAscending_AndResolvesTheName()
    {
        var editionName = "TEST-Edition-" + Guid.NewGuid().ToString("N")[..6];
        await WithUnitOfWorkAsync(async () =>
        {
            using (Tenancy.Change(null))
            {
                var edition = await _editions.InsertAsync(new Edition(Guid.NewGuid(), editionName), autoSave: true);
                var officeB = await OfficeRepository.GetAsync(TenantsTestData.TenantBRef);
                officeB.EditionId = edition.Id;
                await OfficeRepository.UpdateAsync(officeB, autoSave: true);
            }
        });

        var ascending = await OfficeNamesAsync("editionName");
        var descending = await OfficeNamesAsync("editionName desc");

        ascending.IndexOf(TenantsTestData.TenantAName).ShouldBeLessThan(ascending.IndexOf(TenantsTestData.TenantBName));
        descending.IndexOf(TenantsTestData.TenantBName).ShouldBeLessThan(descending.IndexOf(TenantsTestData.TenantAName));
        var page = await OfficePageAsync(new GetOfficesInput { Filter = TenantsTestData.TenantBName });
        page.Items.ShouldHaveSingleItem().EditionName.ShouldBe(editionName);
    }

    [Fact]
    public async Task GetTenantBreakdownAsync_SortsByOfficeName_InBothDirections()
    {
        var ascending = await BreakdownNamesAsync("tenantName");
        var descending = await BreakdownNamesAsync("name desc");

        ascending.IndexOf(TenantsTestData.TenantAName).ShouldBeLessThan(ascending.IndexOf(TenantsTestData.TenantBName));
        descending.IndexOf(TenantsTestData.TenantBName).ShouldBeLessThan(descending.IndexOf(TenantsTestData.TenantAName));
    }

    [Fact]
    public async Task GetTenantBreakdownAsync_SortsByApprovedAndByThisWeek_InBothDirections()
    {
        // Office B gets strictly more Approved rows than office A...
        var before = await BreakdownRowsAsync(null);
        var approvedTopUp = Math.Max(0, Row(before, TenantsTestData.TenantAName).Approved - Row(before, TenantsTestData.TenantBName).Approved) + 1;
        for (var i = 0; i < approvedTopUp; i++)
        {
            await InsertAppointmentAsync(TenantsTestData.TenantBRef, $"S1b{i}", AppointmentStatusType.Approved);
        }

        // ...then office A gets strictly more rows created this week than office B (B's top-up counts too).
        var middle = await BreakdownRowsAsync(null);
        var weekTopUp = Math.Max(0, Row(middle, TenantsTestData.TenantBName).ThisWeek - Row(middle, TenantsTestData.TenantAName).ThisWeek) + 1;
        for (var i = 0; i < weekTopUp; i++)
        {
            await InsertAppointmentAsync(TenantsTestData.TenantARef, $"S1a{i}", AppointmentStatusType.Pending);
        }

        var after = await BreakdownRowsAsync(null);
        Row(after, TenantsTestData.TenantBName).Approved.ShouldBeGreaterThan(Row(after, TenantsTestData.TenantAName).Approved);
        Row(after, TenantsTestData.TenantAName).ThisWeek.ShouldBeGreaterThan(Row(after, TenantsTestData.TenantBName).ThisWeek);

        AssertOrder(await BreakdownNamesAsync("approved"), first: TenantsTestData.TenantAName, second: TenantsTestData.TenantBName);
        AssertOrder(await BreakdownNamesAsync("approved desc"), first: TenantsTestData.TenantBName, second: TenantsTestData.TenantAName);
        AssertOrder(await BreakdownNamesAsync("thisWeek"), first: TenantsTestData.TenantBName, second: TenantsTestData.TenantAName);
        AssertOrder(await BreakdownNamesAsync("thisWeek desc"), first: TenantsTestData.TenantAName, second: TenantsTestData.TenantBName);
    }

    [Fact]
    public async Task GetDashboardAsync_ActivityFeed_DescribesRejectedInfoRequestedAndRescheduled_AndShowsOnlyThisOffice()
    {
        var rejected = await InsertAppointmentAsync(TenantsTestData.TenantARef, "F1rej", AppointmentStatusType.Rejected);
        var info = await InsertAppointmentAsync(TenantsTestData.TenantARef, "F1inf", AppointmentStatusType.InfoRequested);
        var rescheduled = await InsertAppointmentAsync(TenantsTestData.TenantARef, "F1rsc", AppointmentStatusType.RescheduledNoBill);
        // Office decoy: the NEWEST row of all, in office B.
        var otherOffice = await InsertAppointmentAsync(TenantsTestData.TenantBRef, "F1dec", AppointmentStatusType.Rejected);

        var feed = (await GetDashboardAsync(TenantsTestData.TenantARef, DashboardRange.Week)).RecentActivity;

        feed.ShouldNotContain(item => item.Text.StartsWith(otherOffice.ConfirmationNumber));
        ItemFor(feed, rejected).ShouldSatisfyAllConditions(
            i => i.Text.ShouldBe($"{rejected.ConfirmationNumber} was rejected"),
            i => i.Icon.ShouldBe("x"),
            i => i.Tint.ShouldBe("tint-red"));
        ItemFor(feed, info).ShouldSatisfyAllConditions(
            i => i.Text.ShouldBe($"{info.ConfirmationNumber} needs more information"),
            i => i.Icon.ShouldBe("help"),
            i => i.Tint.ShouldBe("tint-amber"));
        ItemFor(feed, rescheduled).ShouldSatisfyAllConditions(
            i => i.Text.ShouldBe($"{rescheduled.ConfirmationNumber} was rescheduled"),
            i => i.Icon.ShouldBe("refresh"),
            i => i.Tint.ShouldBe("tint-blue"));
    }

    [Fact]
    public async Task GetDashboardAsync_QuarterRange_CountsFromTheQuarterStart_AndTheQuarterBeforeAsPrevious()
    {
        var nowUtc = DateTime.UtcNow;
        var quarterStartUtc = new DateTime(nowUtc.Year, ((nowUtc.Month - 1) / 3 * 3) + 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var before = await GetDashboardAsync(TenantsTestData.TenantARef, DashboardRange.Quarter);

        await InsertAppointmentAsync(TenantsTestData.TenantARef, "Q1cur", AppointmentStatusType.Approved, approveDateUtc: quarterStartUtc.AddHours(1));
        await InsertAppointmentAsync(TenantsTestData.TenantARef, "Q1prv", AppointmentStatusType.Approved, approveDateUtc: quarterStartUtc.AddDays(-1));
        // Office decoy: one more approval inside the quarter, in office B.
        await InsertAppointmentAsync(TenantsTestData.TenantBRef, "Q1dec", AppointmentStatusType.Approved, approveDateUtc: quarterStartUtc.AddHours(2));

        var after = await GetDashboardAsync(TenantsTestData.TenantARef, DashboardRange.Quarter);

        (after.ApprovedRequests.Value - before.ApprovedRequests.Value).ShouldBe(1);
        (after.ApprovedRequests.PreviousValue - before.ApprovedRequests.PreviousValue).ShouldBe(1);
    }

    private static DashboardActivityItemDto ItemFor(List<DashboardActivityItemDto> feed, DashboardFixtureRow row) =>
        feed.Where(item => item.Text.StartsWith(row.ConfirmationNumber + " ")).ShouldHaveSingleItem();

    private static DashboardTenantRowDto Row(IEnumerable<DashboardTenantRowDto> rows, string officeName) =>
        rows.Single(r => r.TenantName == officeName);

    private static void AssertOrder(List<string> names, string first, string second) =>
        names.IndexOf(first).ShouldBeLessThan(names.IndexOf(second), $"expected {first} before {second} in [{string.Join(", ", names)}]");

    private async Task<List<DashboardTenantRowDto>> BreakdownRowsAsync(string? sorting)
    {
        using (Tenancy.Change(null))
        {
            return (await Dashboard.GetTenantBreakdownAsync(new GetTenantBreakdownInput { Sorting = sorting })).Items.ToList();
        }
    }

    private async Task<List<string>> BreakdownNamesAsync(string sorting) =>
        (await BreakdownRowsAsync(sorting)).Select(r => r.TenantName).ToList();

    private async Task<PagedResultDto<OfficeListDto>> OfficePageAsync(GetOfficesInput input)
    {
        using (Tenancy.Change(null))
        {
            return await Dashboard.GetOfficesAsync(input);
        }
    }

    private async Task<List<string>> OfficeNamesAsync(string sorting) =>
        (await OfficePageAsync(new GetOfficesInput { Sorting = sorting })).Items.Select(o => o.Name).ToList();
}
