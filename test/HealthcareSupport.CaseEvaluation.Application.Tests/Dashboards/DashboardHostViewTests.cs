using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Locations;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Dashboards;

/// <summary>
/// Pins the host branch of <c>DashboardAppService.GetDashboardAsync</c>.
///
/// <para>Under database-per-office no single connection sees every office, so the
/// host hero tiles are not one cross-office query -- they are a per-office count
/// summed afterwards, and the office table is ordered by volume. Both tests below
/// measure each office first and assert the host figure against the measured pair,
/// so nothing is hardcoded and the rig may hold any number of prior rows.</para>
///
/// <para>NOT pinned here: the host Rejected KPI (same
/// <c>LastModificationTime</c> limitation as the tenant one), and the host
/// Approved KPI's prior-period split, which the tenant-side test already covers
/// through identical window arithmetic.</para>
/// </summary>
public abstract class DashboardHostViewTests<TStartupModule> : DashboardTestsBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    /// <summary>
    /// The host totals are the sum of the offices, and the office table leads with
    /// the busiest office.
    ///
    /// <para>Office A is topped up until it strictly exceeds office B, because the
    /// ordering rule is only observable when the two differ. The totals are then
    /// asserted against both measured values, so dropping either office from the
    /// aggregate fails.</para>
    /// </summary>
    [Fact]
    public async Task GetDashboardAsync_InHostScope_SumsTheOfficesAndLeadsWithTheBusiestOne()
    {
        var topUp = Math.Max(
            0,
            await CountAppointmentsAsync(TenantsTestData.TenantBRef)
                - await CountAppointmentsAsync(TenantsTestData.TenantARef)) + 1;

        for (var index = 0; index < topUp; index++)
        {
            await InsertAppointmentAsync(
                TenantsTestData.TenantARef, $"H1{index}", AppointmentStatusType.Pending);
        }

        var appointmentsA = await CountAppointmentsAsync(TenantsTestData.TenantARef);
        var appointmentsB = await CountAppointmentsAsync(TenantsTestData.TenantBRef);
        var pendingA = await CountPendingAppointmentsAsync(TenantsTestData.TenantARef);
        var pendingB = await CountPendingAppointmentsAsync(TenantsTestData.TenantBRef);
        var doctorsA = await CountDoctorsAsync(TenantsTestData.TenantARef);
        var doctorsB = await CountDoctorsAsync(TenantsTestData.TenantBRef);
        var officeCount = await CountOfficesAsync();

        appointmentsA.ShouldBeGreaterThan(appointmentsB);
        appointmentsB.ShouldBeGreaterThan(0);
        doctorsA.ShouldBeGreaterThan(0);
        doctorsB.ShouldBeGreaterThan(0);

        var dto = await GetDashboardAsync(null, DashboardRange.Week);

        dto.IsHost.ShouldBeTrue();
        dto.TotalTenants.ShouldBe(officeCount);
        dto.TotalAppointments.ShouldBe(appointmentsA + appointmentsB);
        dto.PendingAcrossTenants.ShouldBe(pendingA + pendingB);
        dto.TotalDoctors.ShouldBe(doctorsA + doctorsB);

        dto.Tenants.ShouldContain(r => r.TenantName == TenantsTestData.TenantAName);
        dto.Tenants.ShouldContain(r => r.TenantName == TenantsTestData.TenantBName);
        dto.Tenants[0].TenantName.ShouldBe(TenantsTestData.TenantAName);

        var rowA = dto.Tenants.First(r => r.TenantName == TenantsTestData.TenantAName);
        rowA.Appointments.ShouldBe(appointmentsA);
        rowA.Pending.ShouldBe(pendingA);
    }

    /// <summary>
    /// The host "total locations" tile sums each office's OWN locations, so a
    /// location that belongs to no office is counted nowhere.
    ///
    /// <para>Production never holds such a row (every seeder skips host scope and the
    /// catalog is per-office), so the negative case is built here on purpose: a
    /// host-scoped stray is inserted, and the tile must still move by exactly one when
    /// an office-owned location is added. Counting locations outside the per-office hop
    /// would make the tile a constant multiple of the host-visible rows and the delta
    /// would not be one. (The integration seed used to supply this stray by accident,
    /// by seeding the catalog host-scoped; it now seeds it per office, #764.)</para>
    /// </summary>
    [Fact]
    public async Task GetDashboardAsync_InHostScope_SumsOnlyLocationsThatBelongToAnOffice()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            using (Tenancy.Change(null))
            {
                await LocationRepository.InsertAsync(
                    new Location(
                        id: Guid.NewGuid(),
                        stateId: null,
                        name: $"TEST-DSH-Stray-{Guid.NewGuid().ToString("N")[..8]}",
                        parkingFee: 0m,
                        isActive: true),
                    autoSave: true);
            }
        });

        var hostScopedLocations = await WithUnitOfWorkAsync(async () =>
        {
            using (Tenancy.Change(null))
            {
                return await LocationRepository.CountAsync();
            }
        });
        // The office-less row is the negative this test needs. Without it the
        // delta below would prove nothing about WHICH locations are counted,
        // only that adding one moves the tile.
        hostScopedLocations.ShouldBeGreaterThan(0);

        var before = await GetDashboardAsync(null, DashboardRange.Week);

        await InsertOfficeLocationAsync(
            TenantsTestData.TenantARef,
            $"TEST-DSH-Loc-{Guid.NewGuid().ToString("N")[..8]}");

        var after = await GetDashboardAsync(null, DashboardRange.Week);

        (after.TotalLocations - before.TotalLocations).ShouldBe(1);
    }
}
