using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Enums;
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
    /// catalog row that belongs to no office is counted nowhere.
    ///
    /// <para>The seeded locations are the negative case and they already exist: the
    /// integration seed inserts them host-scoped, so they are visible to a host
    /// query and invisible to every office. The test asserts they exist and that the
    /// tile still moves by exactly one when an office-owned location is added.
    /// Counting locations outside the per-office hop would make the tile a constant
    /// multiple of the host-visible catalog and the delta would be zero.</para>
    ///
    /// <para>This documents current behaviour; it does not claim the behaviour is
    /// right. Whether the location catalog should be per-office or shared is an open
    /// question elsewhere, and it should be settled there rather than by editing
    /// this assertion.</para>
    /// </summary>
    [Fact]
    public async Task GetDashboardAsync_InHostScope_SumsOnlyLocationsThatBelongToAnOffice()
    {
        var hostScopedLocations = await WithUnitOfWorkAsync(async () =>
        {
            using (Tenancy.Change(null))
            {
                return await LocationRepository.CountAsync();
            }
        });
        // The office-less catalog rows are the negative this test needs. Without
        // them the delta below would prove nothing about WHICH locations are
        // counted, only that adding one moves the tile.
        hostScopedLocations.ShouldBeGreaterThan(0);

        var before = await GetDashboardAsync(null, DashboardRange.Week);

        await InsertOfficeLocationAsync(
            TenantsTestData.TenantARef,
            $"TEST-DSH-Loc-{Guid.NewGuid().ToString("N")[..8]}");

        var after = await GetDashboardAsync(null, DashboardRange.Week);

        (after.TotalLocations - before.TotalLocations).ShouldBe(1);
    }
}
