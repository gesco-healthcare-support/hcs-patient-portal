using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Modularity;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Dashboards;

/// <summary>
/// Pins the three host-only registry endpoints: <c>GetTenantSummariesAsync</c>,
/// <c>GetOfficesAsync</c> and <c>GetTenantBreakdownAsync</c>.
///
/// <para>All three read the office registry from the shared management database and
/// then hop into each office for its counts. The interesting rules are in the
/// in-memory layer that sits between: the name filter is trimmed and
/// case-insensitive, the total is taken BEFORE paging (a total taken after paging
/// reads as "1 of 1" and silently hides every other office from the UI), and the
/// breakdown's default sort direction is asymmetric with the explicit one -- no
/// sorting at all means most-active-first, but naming a field without a direction
/// means ascending.</para>
///
/// <para>NOT pinned here: the found-edition arm of the edition-name resolver and the
/// inactive-office arm of <c>IsActive</c>. No <c>Edition</c> row exists in this rig,
/// and making a seeded office passive would mutate registry state every other test
/// in the collection reads. Both are stated as gaps rather than covered by a test
/// that asserts the default and implies the rest.</para>
/// </summary>
public abstract class DashboardOfficeRegistryTests<TStartupModule> : DashboardTestsBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private async Task<List<TenantSummaryDto>> GetSummariesAsync()
    {
        using (Tenancy.Change(null))
        {
            return await Dashboard.GetTenantSummariesAsync();
        }
    }

    private async Task<PagedResultDto<OfficeListDto>> GetOfficePageAsync(
        GetOfficesInput input, Guid? callerOfficeId = null)
    {
        using (Tenancy.Change(callerOfficeId))
        {
            return await Dashboard.GetOfficesAsync(input);
        }
    }

    private async Task<PagedResultDto<DashboardTenantRowDto>> GetBreakdownPageAsync(
        GetTenantBreakdownInput input)
    {
        using (Tenancy.Change(null))
        {
            return await Dashboard.GetTenantBreakdownAsync(input);
        }
    }

    /// <summary>
    /// One summary row per office, ordered by name, with counts taken inside each
    /// office.
    ///
    /// <para>Two appointments go into office A only. Office B's delta of zero is the
    /// half that proves the counts are office-scoped rather than cross-office; A's
    /// user-count delta of zero is the half that proves the two count columns are
    /// not transposed.</para>
    /// </summary>
    [Fact]
    public async Task GetTenantSummariesAsync_ReturnsOfficeScopedCountsOrderedByName()
    {
        var before = await GetSummariesAsync();

        await InsertAppointmentAsync(TenantsTestData.TenantARef, "R1a", AppointmentStatusType.Pending);
        await InsertAppointmentAsync(TenantsTestData.TenantARef, "R1b", AppointmentStatusType.Pending);

        var after = await GetSummariesAsync();

        var beforeA = before.First(s => s.TenantId == TenantsTestData.TenantARef);
        var afterA = after.First(s => s.TenantId == TenantsTestData.TenantARef);
        var beforeB = before.First(s => s.TenantId == TenantsTestData.TenantBRef);
        var afterB = after.First(s => s.TenantId == TenantsTestData.TenantBRef);

        afterA.Name.ShouldBe(TenantsTestData.TenantAName);
        (afterA.AppointmentCount - beforeA.AppointmentCount).ShouldBe(2);
        (afterA.UserCount - beforeA.UserCount).ShouldBe(0);
        (afterB.AppointmentCount - beforeB.AppointmentCount).ShouldBe(0);

        var names = after.Select(s => s.Name).ToList();
        names.IndexOf(TenantsTestData.TenantAName).ShouldBeGreaterThanOrEqualTo(0);
        names.IndexOf(TenantsTestData.TenantAName)
            .ShouldBeLessThan(names.IndexOf(TenantsTestData.TenantBName));
    }

    /// <summary>
    /// The office filter is trimmed and case-insensitive, and the total count is the
    /// FILTERED count taken before paging.
    ///
    /// <para>The filter deliberately carries both surrounding whitespace and the
    /// wrong casing, so trimming and the ordinal-ignore-case comparison are each
    /// required for the single hit. The second half pages down to one row and
    /// asserts the total still reports every office.</para>
    /// </summary>
    [Fact]
    public async Task GetOfficesAsync_TrimsAndCaseFoldsTheFilterAndCountsBeforePaging()
    {
        var officeCount = await CountOfficesAsync();
        officeCount.ShouldBeGreaterThanOrEqualTo(2);

        var filtered = await GetOfficePageAsync(new GetOfficesInput { Filter = "  TENANT-A  " });

        filtered.TotalCount.ShouldBe(1);
        filtered.Items.Count.ShouldBe(1);
        filtered.Items[0].Name.ShouldBe(TenantsTestData.TenantAName);

        var firstPage = await GetOfficePageAsync(new GetOfficesInput { MaxResultCount = 1 });

        firstPage.Items.Count.ShouldBe(1);
        firstPage.TotalCount.ShouldBe(officeCount);
    }

    /// <summary>
    /// The subdomain label is the office name lower-cased, and an office with no
    /// edition reports an empty edition name.
    ///
    /// <para>The seeded office name is mixed case, which is the only reason the
    /// lower-casing is observable at all -- an all-lowercase fixture would pass with
    /// the call deleted.</para>
    /// </summary>
    [Fact]
    public async Task GetOfficesAsync_DerivesTheSubdomainFromTheNameAndLeavesAnUnassignedEditionBlank()
    {
        var page = await GetOfficePageAsync(
            new GetOfficesInput { Filter = TenantsTestData.TenantAName });

        var office = page.Items.ShouldHaveSingleItem();

        office.Id.ShouldBe(TenantsTestData.TenantARef);
        office.Name.ShouldBe(TenantsTestData.TenantAName);
        // The seeded name is mixed case, so name and subdomain must differ. If they
        // ever match, the lower-casing assertion below stops testing anything.
        office.Name.ShouldNotBe(office.Subdomain);
        office.Subdomain.ShouldBe(TenantsTestData.TenantAName.ToLowerInvariant());
        office.EditionId.ShouldBeNull();
        office.EditionName.ShouldBe(string.Empty);
        office.IsActive.ShouldBeTrue();
        office.ConcurrencyStamp.ShouldNotBeNull();
    }

    /// <summary>
    /// Called from inside an office, the endpoint still returns every office, and
    /// each row still carries its OWN counts.
    ///
    /// <para>The appointment goes into the OTHER office, so the row that has to move
    /// is the one the caller's own tenant scope would hide. Dropping the per-office
    /// hop leaves every row reporting whatever the ambient scope sees and the delta
    /// goes to zero.</para>
    ///
    /// <para>Honest label: the outer host-scope switch around the registry read is
    /// not independently pinned here. <c>Tenant</c> and <c>Edition</c> are registry
    /// types that ABP's tenant filter does not touch, so removing that switch
    /// changes nothing observable. This test is behaviour-pinning for the inner hop
    /// and coverage-oriented for the outer one.</para>
    /// </summary>
    [Fact]
    public async Task GetOfficesAsync_CalledInsideAnOfficeStillReturnsEveryOfficeWithItsOwnCounts()
    {
        var before = await GetOfficePageAsync(new GetOfficesInput(), TenantsTestData.TenantARef);

        await InsertAppointmentAsync(TenantsTestData.TenantBRef, "R3b", AppointmentStatusType.Pending);

        var after = await GetOfficePageAsync(new GetOfficesInput(), TenantsTestData.TenantARef);

        after.Items.ShouldContain(o => o.Id == TenantsTestData.TenantARef);
        after.Items.ShouldContain(o => o.Id == TenantsTestData.TenantBRef);

        var beforeB = before.Items.First(o => o.Id == TenantsTestData.TenantBRef);
        var afterB = after.Items.First(o => o.Id == TenantsTestData.TenantBRef);

        (afterB.AppointmentCount - beforeB.AppointmentCount).ShouldBe(1);
    }

    /// <summary>
    /// No sorting at all means most-active office first; naming a field WITHOUT a
    /// direction means ascending.
    ///
    /// <para>This asymmetry is the subtlest rule in the service and the easiest to
    /// "tidy up" into a uniform default. Office A is topped up until it strictly
    /// exceeds office B on both columns, so each of the three calls has a single
    /// unambiguous leader and the three answers are not all the same office.</para>
    /// </summary>
    [Fact]
    public async Task GetTenantBreakdownAsync_DefaultsToBusiestFirstButSortsAscendingOnANamedField()
    {
        var topUp = Math.Max(
            Math.Max(
                0,
                await CountAppointmentsAsync(TenantsTestData.TenantBRef)
                    - await CountAppointmentsAsync(TenantsTestData.TenantARef)),
            Math.Max(
                0,
                await CountPendingAppointmentsAsync(TenantsTestData.TenantBRef)
                    - await CountPendingAppointmentsAsync(TenantsTestData.TenantARef))) + 1;

        for (var index = 0; index < topUp; index++)
        {
            await InsertAppointmentAsync(
                TenantsTestData.TenantARef, $"R5{index}", AppointmentStatusType.Pending);
        }

        (await CountAppointmentsAsync(TenantsTestData.TenantARef))
            .ShouldBeGreaterThan(await CountAppointmentsAsync(TenantsTestData.TenantBRef));
        (await CountPendingAppointmentsAsync(TenantsTestData.TenantARef))
            .ShouldBeGreaterThan(await CountPendingAppointmentsAsync(TenantsTestData.TenantBRef));

        var unsorted = await GetBreakdownPageAsync(new GetTenantBreakdownInput());
        unsorted.Items[0].TenantName.ShouldBe(TenantsTestData.TenantAName);

        var pendingAscending = await GetBreakdownPageAsync(
            new GetTenantBreakdownInput { Sorting = "pending" });
        pendingAscending.Items[0].TenantName.ShouldBe(TenantsTestData.TenantBName);

        var pendingDescending = await GetBreakdownPageAsync(
            new GetTenantBreakdownInput { Sorting = "pending desc" });
        pendingDescending.Items[0].TenantName.ShouldBe(TenantsTestData.TenantAName);
    }

    /// <summary>
    /// The breakdown's filter and count-before-paging contract, which is a separate
    /// code path from the offices list even though the rule is the same.
    /// </summary>
    [Fact]
    public async Task GetTenantBreakdownAsync_FiltersOnOfficeNameAndCountsBeforePaging()
    {
        var officeCount = await CountOfficesAsync();
        officeCount.ShouldBeGreaterThanOrEqualTo(2);

        var filtered = await GetBreakdownPageAsync(
            new GetTenantBreakdownInput { Filter = " TENANT-B " });

        filtered.TotalCount.ShouldBe(1);
        filtered.Items.ShouldHaveSingleItem().TenantName.ShouldBe(TenantsTestData.TenantBName);

        var firstPage = await GetBreakdownPageAsync(
            new GetTenantBreakdownInput { MaxResultCount = 1 });

        firstPage.Items.Count.ShouldBe(1);
        firstPage.TotalCount.ShouldBe(officeCount);
    }
}
