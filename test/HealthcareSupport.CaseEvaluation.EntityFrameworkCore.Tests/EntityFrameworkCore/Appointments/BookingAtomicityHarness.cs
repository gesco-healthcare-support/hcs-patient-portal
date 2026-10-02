using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore.MultiOffice;
using HealthcareSupport.CaseEvaluation.Integration.CaseTracker.SqlServer;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Volo.Saas.Tenants;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Appointments;

/// <summary>
/// Real SQL Server databases for the booking-atomicity tests, on the ONE container the
/// <see cref="SqlServerCollection"/> already runs. A host database and one office database are
/// created and migrated once per test run, and the office is seeded once; each test then starts its
/// own ABP application against them.
///
/// <para><b>No second container.</b> These are extra databases on the shared server, not a new
/// fixture. A second SQL Server container would race the existing one for memory, and the class
/// that lost would be the pre-existing feed suite, failing with an error that points nowhere near
/// the cause.</para>
///
/// <para><b>Why not <c>AbpIntegratedTest</c>.</b> It builds the application in its base
/// constructor, before xUnit has handed the test class its collection fixture, so the application
/// could not be told which server to use. Building it here, after the fixture exists, avoids
/// smuggling the connection string through static state.</para>
/// </summary>
public static class BookingAtomicityHarness
{
    private const string OfficeName = "atomicity-office";

    // Short on purpose: the seeder builds an SSN sentinel "SSN-SENTINEL-{label}", and the column
    // holds 20 characters. "atomicity" overflowed it and failed every test during setup.
    private const string SeedLabel = "atom";

    private static readonly SemaphoreSlim SetupLock = new(1, 1);
    private static Databases? _databases;

    /// <summary>The two connection strings, and the office seeded into the second.</summary>
    public sealed record Databases(string Host, string Office, SeededOffice Seeded);

    /// <summary>
    /// Creates, migrates and seeds the databases on first call; returns the same ones afterwards.
    /// Each migration set runs against its own database -- <c>Migrations/</c> for the host,
    /// <c>TenantMigrations/</c> for the office -- exactly as the DbMigrator applies them.
    /// </summary>
    public static async Task<Databases> GetDatabasesAsync(SqlServerFeedFixture fixture)
    {
        if (_databases != null)
        {
            return _databases;
        }

        await SetupLock.WaitAsync();
        try
        {
            if (_databases != null)
            {
                return _databases;
            }

            var host = Catalog(fixture, "BookingAtomicityHost");
            var office = Catalog(fixture, "BookingAtomicityOffice");

            await using (var hostContext = new CaseEvaluationDbContext(
                new DbContextOptionsBuilder<CaseEvaluationDbContext>().UseSqlServer(host).Options))
            {
                await hostContext.Database.MigrateAsync();
            }
            await using (var officeContext = SqlServerFeedFixture.CreateContext(office))
            {
                await officeContext.Database.MigrateAsync();
            }

            var seeded = await SeedOfficeAsync(host, office);
            _databases = new Databases(host, office, seeded);
            return _databases;
        }
        finally
        {
            SetupLock.Release();
        }
    }

    /// <summary>
    /// Starts a fresh ABP application on <see cref="BookingAtomicityTestModule"/>, whose host scope
    /// resolves to <paramref name="hostConnectionString"/>. The office's own string is read from its
    /// tenant record, as it is in production.
    /// </summary>
    public static async Task<IAbpApplicationWithInternalServiceProvider> StartApplicationAsync(string hostConnectionString)
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.secrets.json", optional: true)
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = hostConnectionString,
            })
            .Build();

        var application = await AbpApplicationFactory.CreateAsync<BookingAtomicityTestModule>(options =>
        {
            options.UseAutofac();
            options.Services.ReplaceConfiguration(configuration);
        });
        await application.InitializeAsync();
        return application;
    }

    private static async Task<SeededOffice> SeedOfficeAsync(string host, string office)
    {
        using var application = await StartApplicationAsync(host);
        var services = application.ServiceProvider;
        var unitOfWorkManager = services.GetRequiredService<IUnitOfWorkManager>();

        Guid officeId;
        using (var uow = unitOfWorkManager.Begin(requiresNew: true, isTransactional: true))
        {
            var tenant = await services.GetRequiredService<ITenantManager>().CreateAsync(OfficeName);
            tenant.SetDefaultConnectionString(office);
            await services.GetRequiredService<IRepository<Tenant, Guid>>().InsertAsync(tenant, autoSave: true);
            officeId = tenant.Id;
            await uow.CompleteAsync();
        }

        SeededOffice seeded;
        using (var uow = unitOfWorkManager.Begin(requiresNew: true, isTransactional: true))
        {
            seeded = await services.GetRequiredService<MultiOfficeSeeder>().SeedAsync(officeId, SeedLabel);
            await uow.CompleteAsync();
        }

        await application.ShutdownAsync();
        return seeded;
    }

    /// <summary>
    /// A connection string for a NEW database on the shared server. Named per run, so a re-run
    /// against a container that outlived a previous run starts from empty databases.
    /// </summary>
    private static string Catalog(SqlServerFeedFixture fixture, string name) =>
        new SqlConnectionStringBuilder(fixture.FeedDatabase)
        {
            InitialCatalog = $"{name}_{Guid.NewGuid():N}",
        }.ConnectionString;

    /// <summary>
    /// Runs <paramref name="body"/> in the office, in a new unit of work that is completed -- for
    /// seeding a slot and for reading back what a booking left behind. Reads always see committed
    /// state only, because the unit of work under test has been disposed by then.
    /// </summary>
    public static async Task InOfficeAsync(
        IServiceProvider services, Guid officeId, Func<Task> body)
    {
        using (services.GetRequiredService<ICurrentTenant>().Change(officeId))
        using (var uow = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true))
        {
            await body();
            await uow.CompleteAsync();
        }
    }
}
