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

    /// <summary>
    /// The day every MultiOffice slot date is measured from, fixed ONCE per process.
    ///
    /// <para><b>Use this, never <c>DateTime.Today</c>, when seeding a slot.</b> The two offices
    /// above are process-wide static, so every test in every MultiOffice class writes against the
    /// same OfficeId and LocationId in one shared database that never rolls back. A slot's identity
    /// is the five-column unique index (TenantId, LocationId, AvailableDate, FromTime, ToTime), and
    /// tests keep those distinct by hand-allocating a different <c>AddDays(N)</c> per test. That
    /// allocation is only collision-free while every test computes its day on the SAME calendar
    /// day: across local midnight, offset N computed after midnight equals offset N+1 computed
    /// before it, so two tests that differ only by one day alias onto one key.</para>
    ///
    /// <para><b>This is not hypothetical.</b> On 2026-09-19T00:00:04Z -- four seconds past UTC
    /// midnight -- <c>MultiOfficeAppointmentsAppServiceTests.CreateAsync_WhenSlotTypesEmpty_AnyTypeWorks</c>
    /// (+20, 09:00-10:00) collided with <c>MultiOfficeAtomicBookingSubmitTests</c>'s
    /// <c>SubmitAsync_WithEveryChildGroup_PersistsAllOfThem</c> (+21, 09:00-10:00) and failed with
    /// <c>SQLite Error 19: UNIQUE constraint failed</c> in CI run 35407439109's sibling Sonar run.
    /// Both offsets resolved to 2026-10-09. Pinning the day here makes that aliasing impossible,
    /// because all offsets are then measured from one value.</para>
    ///
    /// <para>The earlier remedy -- "pick an offset no other test uses" -- addressed the static case
    /// and is blind to this one, since nobody using N says nothing about N+1. It had already been
    /// applied three times and left 45/46/47 as three consecutive 09:00-10:00 offsets.</para>
    ///
    /// <para>Cost of pinning, bounded deliberately: a run crossing midnight leaves seeds one day
    /// staler than production's clock. The tightest seed is +7 against
    /// <c>SystemParameterConsts.DefaultAppointmentLeadTime</c> = 3, so the lead-time gate at
    /// <c>AppointmentBookingValidators.IsSlotWithinLeadTime</c> keeps 3 days of spare margin, and
    /// the skew moves slots AWAY from the max-horizon ceiling rather than toward it.</para>
    /// </summary>
    protected static readonly DateTime TestToday = DateTime.Today;

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
