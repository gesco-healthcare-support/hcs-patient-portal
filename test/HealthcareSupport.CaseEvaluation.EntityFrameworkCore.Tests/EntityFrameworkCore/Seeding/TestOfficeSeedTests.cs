using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Branding;
using HealthcareSupport.CaseEvaluation.Doctors;
using HealthcareSupport.CaseEvaluation.Saas;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Saas.Tenants;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Seeding;

/// <summary>
/// <see cref="OfficeSeedDataContributor"/> in every environment: the host pass registers exactly
/// ONE synthetic office (the tenant, its office-database connection string and its branding), and
/// a rerun adds nothing. <see cref="OfficeSeedData"/> holds only synthetic values, because the repo
/// is public.
///
/// <para>Runs on the office-infrastructure fake from <see cref="DoctorTenantProvisioningTestModule"/>:
/// the rig has no connection-string template, so the real provider throws.</para>
/// </summary>
public class TestOfficeSeedTests : CaseEvaluationTestBase<DoctorTenantProvisioningTestModule>
{
    private readonly OfficeSeedDataContributor _seeder;
    private readonly ITenantRepository _tenants;
    private readonly IRepository<Tenant, Guid> _tenantRows;
    private readonly IRepository<OfficeBranding, Guid> _brandings;
    private readonly ICurrentTenant _currentTenant;

    public TestOfficeSeedTests()
    {
        _seeder = GetRequiredService<OfficeSeedDataContributor>();
        _tenants = GetRequiredService<ITenantRepository>();
        _tenantRows = GetRequiredService<IRepository<Tenant, Guid>>();
        _brandings = GetRequiredService<IRepository<OfficeBranding, Guid>>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    [Fact]
    public void OfficeSeedData_HoldsExactlyOneOffice_WithOnlySyntheticValues()
    {
        var office = OfficeSeedData.Offices.ShouldHaveSingleItem();

        office.TenantName.ShouldStartWith("TEST-");
        office.DisplayName.ShouldStartWith("TEST ");
        office.DoctorFirstName.ShouldStartWith("TEST-");
        office.DoctorLastName.ShouldStartWith("TEST-");
        office.AdminEmail.ShouldEndWith("@example.test");
        office.DoctorEmail.ShouldEndWith("@example.test");
        OfficeSeedData.TestOffice.ShouldBeSameAs(office);
    }

    [Fact]
    public async Task HostPass_RegistersExactlyOneTestOffice_WithItsDatabaseAndBranding_AndARerunAddsNothing()
    {
        var office = OfficeSeedData.TestOffice;
        var before = await InHostAsync(() => _tenantRows.GetListAsync());

        await InHostAsync(async () => { await _seeder.SeedAsync(new DataSeedContext()); return true; });
        await InHostAsync(async () => { await _seeder.SeedAsync(new DataSeedContext()); return true; });

        var after = await InHostAsync(() => _tenantRows.GetListAsync());
        after.Count.ShouldBe(before.Count + 1);
        // Decoy: the rig's own offices are still there, untouched.
        after.ShouldContain(t => t.Id == TenantsTestData.TenantARef && t.Name == TenantsTestData.TenantAName);

        var registered = after.Where(t => t.Name == office.TenantName).ShouldHaveSingleItem();
        var withDetails = await InHostAsync(() => _tenants.GetAsync(registered.Id));
        withDetails.FindDefaultConnectionString().ShouldBe(FakeOfficeInfrastructure.ConnectionStringFor(office.Slug));
        var branding = (await InHostAsync(() => _brandings.GetListAsync(b => b.OfficeId == registered.Id))).ShouldHaveSingleItem();
        branding.DisplayName.ShouldBe(office.DisplayName);
    }

    [Fact]
    public async Task OfficePass_RegistersNothing()
    {
        var before = await InHostAsync(() => _tenantRows.GetCountAsync());

        await InHostAsync(async () => { await _seeder.SeedAsync(new DataSeedContext(TenantsTestData.TenantARef)); return true; });

        (await InHostAsync(() => _tenantRows.GetCountAsync())).ShouldBe(before);
    }

    private Task<T> InHostAsync<T>(Func<Task<T>> action) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(null))
            {
                return await action();
            }
        });
}
