using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Branding;
using HealthcareSupport.CaseEvaluation.Data;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using HealthcareSupport.CaseEvaluation.Practices;
using HealthcareSupport.CaseEvaluation.TestData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Saas.Host.Dtos;
using Volo.Saas.Tenants;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Doctors;

/// <summary>
/// Stands in for the three pieces of real infrastructure <see cref="DoctorTenantAppService"/>
/// needs to create an office: the connection-string template, the database-server reachability
/// check, and the office-database provisioner. One instance per test application (a singleton in
/// that application's container), so nothing is shared between tests that run in parallel.
/// </summary>
public sealed class FakeOfficeInfrastructure : ITenantConnectionStringProvider, IConnectionStringChecker, IOfficeDatabaseProvisioner
{
    public bool DatabaseServerReachable { get; set; } = true;

    /// <summary>
    /// When set, the NEW PRACTICE call to the provisioner throws it. That call is recognised by
    /// its doctor fields, so the tenant-created event's migration handler (which provisions with
    /// no doctor fields) is not failed by accident.
    /// </summary>
    public Exception? PracticeProvisioningFailure { get; set; }

    public List<ProvisionCall> Calls { get; } = new();

    public string BuildConnectionString(string slug) => ConnectionStringFor(slug);

    public static string ConnectionStringFor(string slug) => "TEST-office-connection-" + slug;

    public Task<AbpConnectionStringCheckResult> CheckAsync(string connectionString) =>
        Task.FromResult(new AbpConnectionStringCheckResult { Connected = DatabaseServerReachable, DatabaseExists = false });

    public Task ProvisionAsync(Guid tenantId, string adminEmailAddress, string adminPassword,
        string? doctorFirstName = null, string? doctorLastName = null, string? doctorEmail = null)
    {
        if (PracticeProvisioningFailure != null && doctorFirstName != null)
        {
            throw PracticeProvisioningFailure;
        }

        Calls.Add(new ProvisionCall(tenantId, adminEmailAddress, doctorFirstName, doctorLastName, doctorEmail));
        return Task.CompletedTask;
    }

    public sealed record ProvisionCall(Guid TenantId, string AdminEmail, string? DoctorFirstName, string? DoctorLastName, string? DoctorEmail);
}

/// <summary>
/// The EF Core test graph with the office infrastructure replaced by <see cref="FakeOfficeInfrastructure"/>.
/// Without it, <c>ITenantConnectionStringProvider.BuildConnectionString</c> throws in the test
/// harness (no connection template), so nothing after the first line of the office-creation path
/// was reachable (see <c>DoctorTenantAppServiceTests</c>' remarks).
/// </summary>
[DependsOn(typeof(CaseEvaluationEntityFrameworkCoreTestModule))]
public class DoctorTenantProvisioningTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddSingleton<FakeOfficeInfrastructure>();
        context.Services.Replace(ServiceDescriptor.Transient<ITenantConnectionStringProvider>(sp => sp.GetRequiredService<FakeOfficeInfrastructure>()));
        context.Services.Replace(ServiceDescriptor.Transient<IConnectionStringChecker>(sp => sp.GetRequiredService<FakeOfficeInfrastructure>()));
        context.Services.Replace(ServiceDescriptor.Transient<IOfficeDatabaseProvisioner>(sp => sp.GetRequiredService<FakeOfficeInfrastructure>()));
    }
}

/// <summary>
/// <see cref="DoctorTenantAppService"/>'s office-creation path past the naming guard: the
/// reachability pre-check, the tenant row plus its connection string, the provisioning call and
/// its compensation message, and the host-side branding display name.
///
/// <para>The branding upsert looks the office up by id, so the happy path carries an OFFICE
/// decoy: TenantA's own branding row must be untouched when a new office is branded.</para>
/// </summary>
public class DoctorTenantProvisioningTests : CaseEvaluationTestBase<DoctorTenantProvisioningTestModule>
{
    private readonly DoctorTenantAppService _service;
    private readonly FakeOfficeInfrastructure _office;
    private readonly ITenantRepository _tenants;
    private readonly IRepository<Tenant, Guid> _tenantStore;
    private readonly IRepository<OfficeBranding, Guid> _brandings;
    private readonly ICurrentTenant _currentTenant;

    public DoctorTenantProvisioningTests()
    {
        _service = GetRequiredService<DoctorTenantAppService>();
        _office = GetRequiredService<FakeOfficeInfrastructure>();
        _tenants = GetRequiredService<ITenantRepository>();
        _tenantStore = GetRequiredService<IRepository<Tenant, Guid>>();
        _brandings = GetRequiredService<IRepository<OfficeBranding, Guid>>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    [Fact]
    public async Task CreatePractice_ProvisionsTheOffice_StoresItsConnectionString_AndBrandsOnlyThatOffice()
    {
        // Office decoy: TenantA already has a branding row with its own display name.
        await InHostAsync(async () =>
        {
            var existing = new OfficeBranding(Guid.NewGuid(), TenantsTestData.TenantARef);
            existing.SetDisplayName("TEST-Office A Display");
            await _brandings.InsertAsync(existing, autoSave: true);
            return true;
        });
        var slug = NewSlug();

        var created = await _service.CreatePracticeAsync(new CreatePracticeInput
        {
            Slug = slug,
            DoctorFirstName = "TEST-Ada",
            DoctorLastName = "TEST-Lovelace",
            DoctorEmail = $"  {slug}@test.local ",
        });

        created.Name.ShouldBe(slug);
        _office.Calls.ShouldContain(new FakeOfficeInfrastructure.ProvisionCall(
            created.Id, $"{slug}@test.local", "TEST-Ada", "TEST-Lovelace", $"{slug}@test.local"));
        var tenant = await InHostAsync(() => _tenants.GetAsync(created.Id));
        tenant.FindDefaultConnectionString().ShouldBe(FakeOfficeInfrastructure.ConnectionStringFor(slug));
        (await BrandingFor(TenantsTestData.TenantARef)).ShouldBe("TEST-Office A Display");
        (await BrandingFor(created.Id)).ShouldBe(PracticeNaming.DefaultDisplayName("TEST-Ada", "TEST-Lovelace"));
    }

    [Fact]
    public async Task CreatePractice_WithADisplayName_BrandsTheOfficeWithItTrimmed()
    {
        var slug = NewSlug();

        var created = await _service.CreatePracticeAsync(new CreatePracticeInput
        {
            Slug = slug,
            DoctorFirstName = "TEST-Grace",
            DoctorLastName = "TEST-Hopper",
            DoctorEmail = $"{slug}@test.local",
            DisplayName = "  TEST Hopper Orthopedics  ",
        });

        (await BrandingFor(created.Id)).ShouldBe("TEST Hopper Orthopedics");
    }

    [Fact]
    public async Task CreatePractice_WhenTheDatabaseServerIsUnreachable_CreatesNoTenantAndProvisionsNothing()
    {
        _office.DatabaseServerReachable = false;
        var slug = NewSlug();

        var ex = await Should.ThrowAsync<UserFriendlyException>(() => _service.CreatePracticeAsync(new CreatePracticeInput
        {
            Slug = slug,
            DoctorFirstName = "TEST-Alan",
            DoctorLastName = "TEST-Turing",
            DoctorEmail = $"{slug}@test.local",
        }));

        ex.Message.ShouldStartWith($"Cannot reach the database server for office '{slug}'");
        (await InHostAsync(() => _tenantStore.FirstOrDefaultAsync(t => t.Name == slug))).ShouldBeNull();
        (await InHostAsync(() => _tenantStore.FirstOrDefaultAsync(t => t.Name == TenantsTestData.TenantAName))).ShouldNotBeNull();
        _office.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task CreatePractice_WhenProvisioningFails_KeepsTheTenantForARetry_AndSaysSo()
    {
        _office.PracticeProvisioningFailure = new InvalidOperationException("TEST-provisioning-down");
        var slug = NewSlug();

        var ex = await Should.ThrowAsync<UserFriendlyException>(() => _service.CreatePracticeAsync(new CreatePracticeInput
        {
            Slug = slug,
            DoctorFirstName = "TEST-Edsger",
            DoctorLastName = "TEST-Dijkstra",
            DoctorEmail = $"{slug}@test.local",
        }));

        ex.Message.ShouldBe($"Office '{slug}' was created but its database could not be fully provisioned. Re-run provisioning to complete setup.");
        var kept = await InHostAsync(() => _tenantStore.FirstOrDefaultAsync(t => t.Name == slug));
        // Reload through the Saas repository: it includes the connection strings the plain one leaves unloaded.
        var tenant = await InHostAsync(() => _tenants.GetAsync(kept.ShouldNotBeNull().Id));
        tenant.FindDefaultConnectionString().ShouldBe(FakeOfficeInfrastructure.ConnectionStringFor(slug));
        (await BrandingFor(tenant.Id)).ShouldBeNull();
    }

    [Fact]
    public async Task StockCreate_ProvisionsTheOfficeWithoutDoctorFields()
    {
        var slug = NewSlug();

        var created = await _service.CreateAsync(new SaasTenantCreateDto
        {
            Name = slug,
            AdminEmailAddress = $"{slug}@test.local",
            AdminPassword = "TEST-Passw0rd!",
        });

        _office.Calls.ShouldContain(new FakeOfficeInfrastructure.ProvisionCall(created.Id, $"{slug}@test.local", null, null, null));
        (await InHostAsync(() => _tenants.GetAsync(created.Id))).FindDefaultConnectionString()
            .ShouldBe(FakeOfficeInfrastructure.ConnectionStringFor(slug));
    }

    private Task<string?> BrandingFor(Guid officeId) =>
        InHostAsync(async () => (await _brandings.FirstOrDefaultAsync(b => b.OfficeId == officeId))?.DisplayName);

    private Task<T> InHostAsync<T>(Func<Task<T>> action) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(null))
            {
                return await action();
            }
        });

    private static string NewSlug() => "testoffice" + Guid.NewGuid().ToString("N")[..10];
}
