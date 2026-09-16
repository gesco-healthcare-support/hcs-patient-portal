using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.RealAuthorization;

/// <summary>
/// Named in-memory SQLite databases backing the real-authorization harness (#707 layer 3).
///
/// <para>DELIBERATELY SEPARATE FROM <c>MultiOfficeTestDatabase</c> RATHER THAN REUSING IT.
/// This harness seeds roles, permission grants and users that the multi-office isolation
/// tests do not expect, and xUnit runs distinct collections in parallel by default. Sharing
/// their databases would let this harness's rows appear inside an isolation assertion that
/// counts rows, which would present as a flaky cross-office leak -- the single most
/// expensive kind of false positive this repository could grow.</para>
///
/// <para>The mechanics (named shared-cache plus a keeper connection held open for the
/// process lifetime) are the same as <c>MultiOfficeTestDatabase</c> and are explained there
/// in full; the short version is that a bare <c>:memory:</c> database is private to one
/// connection and is discarded the instant that connection closes.</para>
/// </summary>
public static class RealAuthorizationTestDatabase
{
    public const string HostConnectionString =
        "Data Source=F2AuthzHost;Mode=Memory;Cache=Shared;Foreign Keys=True";

    public const string OfficeConnectionString =
        "Data Source=F2AuthzOffice;Mode=Memory;Cache=Shared;Foreign Keys=True";

    private static readonly object SyncRoot = new();
    private static readonly List<SqliteConnection> Keepers = new();
    private static bool _initialized;

    /// <summary>
    /// Opens a keeper connection per database and creates the schema in each. Idempotent
    /// and process-wide. Keepers are never disposed during the run; the OS reclaims the
    /// in-memory databases at process exit.
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

            // Host database carries the SaaS tenant tables that tenant creation needs.
            CreateSchema<CaseEvaluationDbContext>(
                HostConnectionString, options => new CaseEvaluationDbContext(options));

            // Office database is tenant-shaped, matching what production's tenant
            // migration creates.
            CreateSchema<CaseEvaluationTenantDbContext>(
                OfficeConnectionString, options => new CaseEvaluationTenantDbContext(options));

            _initialized = true;
        }
    }

    private static void CreateSchema<TDbContext>(
        string connectionString,
        Func<DbContextOptions<TDbContext>, TDbContext> contextFactory)
        where TDbContext : DbContext
    {
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
