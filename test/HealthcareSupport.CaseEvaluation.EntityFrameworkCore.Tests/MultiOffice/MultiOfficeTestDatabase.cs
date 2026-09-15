using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.MultiOffice;

/// <summary>
/// Process-lifetime registry of the named in-memory SQLite databases that back the
/// multi-office isolation harness (Phase F / F1). Each office -- and the host -- gets
/// its OWN database, so a test can prove that office A's data is physically absent
/// from office B's database, not merely hidden by ABP's IMultiTenant query filter.
///
/// Why named shared-cache (F-8): a bare <c>:memory:</c> connection owns a private,
/// unshareable database that vanishes the moment that single connection closes.
/// <c>Data Source=name;Mode=Memory;Cache=Shared</c> names the database so every
/// connection opened with the SAME string shares ONE in-memory database -- but only
/// while at least one connection stays open. We therefore hold a "keeper" connection
/// open for the whole test run; without it the shared database would be discarded
/// between units of work and every per-office connection would silently get a fresh,
/// empty database (a false-positive "isolation" pass).
///
/// Routing mirrors production exactly: ABP's stock MultiTenantConnectionStringResolver
/// reads the connection string stored on each tenant record
/// (<c>tenant.SetDefaultConnectionString(...)</c>, as DoctorTenantAppService does), so
/// the harness needs no custom resolver -- only these distinct connection strings and
/// the matching <c>UseSqlite(context.ConnectionString)</c> registration in the module.
/// </summary>
public static class MultiOfficeTestDatabase
{
    // "Foreign Keys=True" so NoAction/cascade delete constraints are enforced (the
    // SQLite default is OFF), matching the single-connection harness.
    public const string HostConnectionString =
        "Data Source=F1MultiOfficeHost;Mode=Memory;Cache=Shared;Foreign Keys=True";
    public const string OfficeAConnectionString =
        "Data Source=F1MultiOfficeA;Mode=Memory;Cache=Shared;Foreign Keys=True";
    public const string OfficeBConnectionString =
        "Data Source=F1MultiOfficeB;Mode=Memory;Cache=Shared;Foreign Keys=True";

    private static readonly object SyncRoot = new();
    private static readonly List<SqliteConnection> Keepers = new();
    private static bool _initialized;

    /// <summary>
    /// Opens a keeper connection per database (host + every office) and creates the
    /// full schema in each. Idempotent and process-wide: runs once no matter how many
    /// test classes load the multi-office module. Keepers are intentionally never
    /// disposed during the run -- the OS reclaims the in-memory databases at process
    /// exit, which also sidesteps the known Microsoft.Data.Sqlite Dispose() race.
    /// </summary>
    public static void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        lock (SyncRoot)
        {
            if (_initialized)
            {
                return;
            }

            // Host database: host-shaped schema (CaseEvaluationDbContext with no current
            // tenant => IsHostDatabase() == true), so it carries the SaaS tenant tables
            // that tenant creation needs plus the host-only entities.
            CreateSchema<CaseEvaluationDbContext>(
                HostConnectionString, options => new CaseEvaluationDbContext(options));

            // Office databases: tenant-shaped schema (CaseEvaluationTenantDbContext),
            // matching what production's tenant migration creates -- no SaaS tenant table
            // and no Tenant foreign key on operational entities. Creating them with the
            // host context instead would bake in a Doctor->SaasTenants FK that an office
            // database (with an empty SaasTenants table) cannot satisfy.
            foreach (var officeConnectionString in new[] { OfficeAConnectionString, OfficeBConnectionString })
            {
                CreateSchema<CaseEvaluationTenantDbContext>(
                    officeConnectionString, options => new CaseEvaluationTenantDbContext(options));
            }

            _initialized = true;
        }
    }

    private static void CreateSchema<TDbContext>(
        string connectionString,
        Func<DbContextOptions<TDbContext>, TDbContext> contextFactory)
        where TDbContext : DbContext
    {
        // Hold the keeper open for the process lifetime so the named shared-cache
        // database survives between units of work.
        var keeper = new SqliteConnection(connectionString);
        keeper.Open();
        Keepers.Add(keeper);

        var options = new DbContextOptionsBuilder<TDbContext>()
            .UseSqlite(keeper)
            .Options;
        using var context = contextFactory(options);
        context.GetService<IRelationalDatabaseCreator>().CreateTables();
    }
}
