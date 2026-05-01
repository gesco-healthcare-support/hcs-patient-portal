using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Sqlite;
using Volo.Abp.FeatureManagement;
using Volo.Abp.Modularity;
using Volo.Abp.PermissionManagement;
using Volo.Abp.TextTemplateManagement;
using Volo.Abp.Uow;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore;

[DependsOn(
    typeof(CaseEvaluationApplicationTestModule),
    typeof(CaseEvaluationEntityFrameworkCoreModule),
    typeof(AbpEntityFrameworkCoreSqliteModule)
)]
public class CaseEvaluationEntityFrameworkCoreTestModule : AbpModule
{
    private SqliteConnection? _sqliteConnection;

    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        PreConfigure<AbpSqliteOptions>(x => x.BusyTimeout = null);
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<FeatureManagementOptions>(options =>
        {
            options.SaveStaticFeaturesToDatabase = false;
            options.IsDynamicFeatureStoreEnabled = false;
        });
        Configure<PermissionManagementOptions>(options =>
        {
            options.SaveStaticPermissionsToDatabase = false;
            options.IsDynamicPermissionStoreEnabled = false;
        });
        Configure<TextTemplateManagementOptions>(options =>
        {
            options.SaveStaticTemplatesToDatabase = false;
            options.IsDynamicTemplateStoreEnabled = false;
        });
        context.Services.AddAlwaysDisableUnitOfWorkTransaction();

        ConfigureInMemorySqlite(context.Services);

    }

    private void ConfigureInMemorySqlite(IServiceCollection services)
    {
        _sqliteConnection = CreateDatabaseAndGetConnection();

        services.Configure<AbpDbContextOptions>(options =>
        {
            options.Configure(context =>
            {
                context.DbContextOptions.UseSqlite(_sqliteConnection);
            });
        });
    }

    // Microsoft.Data.Sqlite intermittently throws NullReferenceException from
    // SqliteConnection.Close() during Dispose() when the connection's internal
    // command pool is being torn down concurrently with an in-flight EF Core
    // unit-of-work rollback. This is a library-side race that has persisted
    // across versions without an upstream fix:
    //   https://github.com/aspnet/Microsoft.Data.Sqlite/issues/466
    //   https://github.com/dotnet/efcore/issues/20651
    //   https://github.com/abpframework/abp/issues/19065
    // ABP 10 does not expose an OnPostApplicationShutdown hook (the pre/post
    // variants exist on the Initialization side only), so we cannot defer the
    // disposal to a later lifecycle phase. The test has already completed by
    // the time this hook runs, so swallowing the shutdown-time NRE is safe
    // and prevents the flake from failing deploy-dev.yml's Validate job.
    public override void OnApplicationShutdown(ApplicationShutdownContext context)
    {
        try
        {
            _sqliteConnection?.Dispose();
        }
        catch (NullReferenceException)
        {
            // Intentional: known Microsoft.Data.Sqlite Dispose race.
        }
        finally
        {
            _sqliteConnection = null;
        }
    }

    private static SqliteConnection CreateDatabaseAndGetConnection()
    {
        // Microsoft.Data.Sqlite defaults FK enforcement OFF and EF Core's
        // per-connection PRAGMA only fires when EF Core itself opens the
        // connection. We open manually here to keep the in-memory DB alive
        // across the test run, so we enable FK enforcement explicitly:
        //   - "Foreign Keys=True" handles the fresh-open path.
        //   - The explicit PRAGMA below is belt-and-suspenders in case ABP /
        //     EF pool a wrapper that bypasses the connection-string opt-in.
        // Without these, NoAction delete constraints exercised in Locations
        // delete-constraint tests are silently skipped by the test DB.
        var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        connection.Open();

        using (var pragmaCommand = connection.CreateCommand())
        {
            pragmaCommand.CommandText = "PRAGMA foreign_keys = ON;";
            pragmaCommand.ExecuteNonQuery();
        }

        var options = new DbContextOptionsBuilder<CaseEvaluationDbContext>()
            .UseSqlite(connection)
            .Options;

        using (var context = new CaseEvaluationDbContext(options))
        {
            context.GetService<IRelationalDatabaseCreator>().CreateTables();
        }

        return connection;
    }
}
