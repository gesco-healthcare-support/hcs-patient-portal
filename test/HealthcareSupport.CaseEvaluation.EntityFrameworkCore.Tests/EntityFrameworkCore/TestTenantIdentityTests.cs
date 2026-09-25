using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Saas.Tenants;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore;

/// <summary>
/// Guards the fixed test-tenant ids (#1034). <see cref="TenantsTestData.TenantARef"/> and
/// <see cref="TenantsTestData.TenantBRef"/> are read by 77 test files, and every test builds
/// its own application and its own database. So the ids are only trustworthy if EVERY
/// application's seed creates its tenants with exactly those values. When the seed captured
/// generated ids into a static instead, a parallel run let one application overwrite the id
/// another was about to read, and that id did not exist in the reader's database.
///
/// <para>These facts read the tenant rows back from THIS test's own database by the fixed ids.
/// If the seed ever stops pinning the id, it throws first (see
/// <c>CaseEvaluationIntegrationTestSeedContributor.CreateTenantWithFixedIdAsync</c>). If the seed
/// kept going with generated ids, the lookups here would come back empty.</para>
/// </summary>
public class TestTenantIdentityTests : CaseEvaluationEntityFrameworkCoreTestBase
{
    [Fact]
    public async Task TenantRefs_ResolveToTheSeededTenants_InThisTestsOwnDatabase()
    {
        var repository = GetRequiredService<IRepository<Tenant, Guid>>();
        var currentTenant = GetRequiredService<ICurrentTenant>();

        await WithUnitOfWorkAsync(async () =>
        {
            using (currentTenant.Change(null))
            {
                var tenantA = await repository.FindAsync(TenantsTestData.TenantARef);
                var tenantB = await repository.FindAsync(TenantsTestData.TenantBRef);

                tenantA.ShouldNotBeNull();
                tenantA.Name.ShouldBe(TenantsTestData.TenantAName);
                tenantB.ShouldNotBeNull();
                tenantB.Name.ShouldBe(TenantsTestData.TenantBName);
            }
        });
    }

    [Fact]
    public void TenantRefs_AreFixedAndDistinct()
    {
        TenantsTestData.TenantARef.ShouldBe(Guid.Parse("d1a00000-0000-0000-0000-00000000000a"));
        TenantsTestData.TenantBRef.ShouldBe(Guid.Parse("d1b00000-0000-0000-0000-00000000000b"));
        TenantsTestData.TenantARef.ShouldNotBe(TenantsTestData.TenantBRef);
    }
}
