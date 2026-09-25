using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Identity;
using HealthcareSupport.CaseEvaluation.Saas;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Seeding;

/// <summary>
/// The office admin that ABP's identity seed creates for every tenant is NOT created for the
/// synthetic TEST office outside Development. That seed falls back to a publicly known default
/// password, and the TEST office also exists in production, so its admin would be a known
/// credential. The host admin invites the TEST office's staff instead.
///
/// <para>Every other office is the decoy: it must still get its admin. The environment comes from
/// ABP's <see cref="IAbpHostEnvironment"/>, which the rig leaves at its Production default.</para>
/// </summary>
public class TestOfficeAdminSeedTests : SeedContributorTestBase
{
    [Fact]
    public async Task OutsideDevelopment_TheTestOfficeGetsNoAdmin_ButAnyOtherOfficeDoes()
    {
        var testOffice = await CreatePracticeAsync(OfficeSeedData.TestOffice.TenantName);
        var otherOffice = await CreatePracticeAsync("TEST-practice-" + Guid.NewGuid().ToString("N")[..10]);
        var seeder = GetRequiredService<IdentityDataSeedContributor>();

        await SeedAsync(seeder, AdminContext(testOffice));
        await SeedAsync(seeder, AdminContext(otherOffice));

        seeder.ShouldBeOfType<CaseEvaluationIdentityDataSeedContributor>();
        (await UsersOf(testOffice)).ShouldBeEmpty();
        (await UsersOf(otherOffice)).ShouldHaveSingleItem().Email.ShouldBe("test.admin@example.test");
    }

    [Fact]
    public void TheReplacementRunsOnce_UnderTheFrameworkContributorsEntry()
    {
        var contributors = GetRequiredService<IOptions<AbpDataSeedOptions>>().Value.Contributors;

        contributors.ShouldContain(typeof(IdentityDataSeedContributor));
        contributors.ShouldNotContain(typeof(CaseEvaluationIdentityDataSeedContributor));
    }

    internal static DataSeedContext AdminContext(Guid officeId) =>
        new DataSeedContext(officeId)
            .WithProperty(IdentityDataSeedContributor.AdminEmailPropertyName, "test.admin@example.test")
            .WithProperty(IdentityDataSeedContributor.AdminPasswordPropertyName, "TEST-Passw0rd!");

    private Task<System.Collections.Generic.List<IdentityUser>> UsersOf(Guid officeId) =>
        InScopeAsync(officeId, () => GetRequiredService<IRepository<IdentityUser, Guid>>().GetListAsync());
}

/// <summary>
/// In Development the TEST office keeps its admin, so a fresh local stack can still sign in to it.
/// </summary>
public class TestOfficeAdminSeedDevelopmentTests : SeedContributorTestBase
{
    protected override void SetAbpApplicationCreationOptions(AbpApplicationCreationOptions options)
    {
        base.SetAbpApplicationCreationOptions(options);
        options.Environment = Environments.Development;
    }

    [Fact]
    public async Task InDevelopment_TheTestOfficeGetsItsAdmin()
    {
        var testOffice = await CreatePracticeAsync(OfficeSeedData.TestOffice.TenantName);

        await SeedAsync(GetRequiredService<IdentityDataSeedContributor>(), TestOfficeAdminSeedTests.AdminContext(testOffice));

        (await InScopeAsync(testOffice, () => GetRequiredService<IRepository<IdentityUser, Guid>>().GetListAsync()))
            .ShouldHaveSingleItem().Email.ShouldBe("test.admin@example.test");
    }
}
