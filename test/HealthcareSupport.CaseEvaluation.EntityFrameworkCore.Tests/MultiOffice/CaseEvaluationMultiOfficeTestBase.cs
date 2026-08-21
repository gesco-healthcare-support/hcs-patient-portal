using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Testing;
using Volo.Abp.Uow;
using Volo.Saas.Tenants;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.MultiOffice;

/// <summary>
/// Base class for tests that run on the multi-office isolation harness. Overrides the
/// host "Default" connection string to point at the in-memory host database, exposes
/// the standard WithUnitOfWorkAsync helpers, and lazily creates + seeds two offices that
/// every isolation test shares.
/// </summary>
public abstract class CaseEvaluationMultiOfficeTestBase
    : AbpIntegratedTest<CaseEvaluationMultiOfficeTestModule>
{
    public const string OfficeAName = "F-office-a";
    public const string OfficeBName = "F-office-b";

    // The two offices are created + seeded ONCE for the whole test run: the named
    // databases and the tenant records live in process-wide static state, so re-creating
    // them per test class would collide on tenant-name uniqueness. The SemaphoreSlim
    // serializes the first-time seed across the (collection-serialized) test classes.
    private static readonly SemaphoreSlim SeedLock = new(1, 1);
    private static (SeededOffice A, SeededOffice B)? _seededOffices;

    protected override void SetAbpApplicationCreationOptions(AbpApplicationCreationOptions options)
    {
        options.UseAutofac();
    }

    protected override void BeforeAddApplication(IServiceCollection services)
    {
        var builder = new ConfigurationBuilder();
        builder.AddJsonFile("appsettings.json", optional: false);
        builder.AddJsonFile("appsettings.secrets.json", optional: true);
        // Point host scope (CurrentTenant == null) at the in-memory host database so
        // UseSqlite(context.ConnectionString) opens it rather than the SQL Server
        // string baked into appsettings.json.
        builder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = MultiOfficeTestDatabase.HostConnectionString,
        });
        services.ReplaceConfiguration(builder.Build());
    }

    /// <summary>
    /// Returns the two shared, fully-seeded offices, creating them on first call. Office
    /// A routes to the OfficeA database, office B to the OfficeB database.
    /// </summary>
    protected async Task<(SeededOffice A, SeededOffice B)> GetSeededOfficesAsync()
    {
        if (_seededOffices != null)
        {
            return _seededOffices.Value;
        }

        await SeedLock.WaitAsync();
        try
        {
            if (_seededOffices != null)
            {
                return _seededOffices.Value;
            }

            var currentTenant = GetRequiredService<ICurrentTenant>();
            var tenantManager = GetRequiredService<ITenantManager>();
            var tenantRepository = GetRequiredService<IRepository<Tenant, Guid>>();
            var seeder = GetRequiredService<MultiOfficeSeeder>();

            var result = await WithUnitOfWorkAsync(async () =>
            {
                Guid officeAId;
                Guid officeBId;
                using (currentTenant.Change(null))
                {
                    var a = await tenantManager.CreateAsync(OfficeAName);
                    a.SetDefaultConnectionString(MultiOfficeTestDatabase.OfficeAConnectionString);
                    await tenantRepository.InsertAsync(a, autoSave: true);
                    officeAId = a.Id;

                    var b = await tenantManager.CreateAsync(OfficeBName);
                    b.SetDefaultConnectionString(MultiOfficeTestDatabase.OfficeBConnectionString);
                    await tenantRepository.InsertAsync(b, autoSave: true);
                    officeBId = b.Id;
                }

                var seededA = await seeder.SeedAsync(officeAId, "officeA");
                var seededB = await seeder.SeedAsync(officeBId, "officeB");
                return (seededA, seededB);
            }, requiresNew: true);

            _seededOffices = result;
            return result;
        }
        finally
        {
            SeedLock.Release();
        }
    }

    // requiresNew starts a fresh, independent unit of work -- needed when crossing
    // into a different tenant's context so the office connection re-resolves (per ABP
    // GitHub #16357 / the project's B9 finding).
    protected virtual async Task WithUnitOfWorkAsync(Func<Task> action, bool requiresNew = false)
    {
        using var scope = ServiceProvider.CreateScope();
        var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
        using var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew);
        await action();
        await uow.CompleteAsync();
    }

    protected virtual async Task<TResult> WithUnitOfWorkAsync<TResult>(
        Func<Task<TResult>> func, bool requiresNew = false)
    {
        using var scope = ServiceProvider.CreateScope();
        var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
        using var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew);
        var result = await func();
        await uow.CompleteAsync();
        return result;
    }
}
