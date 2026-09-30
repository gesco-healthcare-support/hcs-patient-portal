using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Testcontainers.MsSql;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker.SqlServer;

/// <summary>
/// A real SQL Server for the feed's SQL (#927): rowversion and <c>MIN_ACTIVE_ROWVERSION()</c> exist nowhere else,
/// and the SQLite rig cannot generate them. Started once per test class from the image docker-compose.yml pins,
/// so the tests run against the engine the portal runs on. Docker must be running.
///
/// <para>Two databases, both built by the TENANT migrations -- office databases are what the feed reads, and
/// applying the real migrations here is also the proof that <c>Added_CaseTrackerFeed</c> applies on SQL
/// Server:</para>
/// <list type="bullet">
///   <item><see cref="FeedDatabase"/>, migrated to the latest, with READ_COMMITTED_SNAPSHOT on (Azure SQL's
///   default). There an open transaction's rows are invisible rather than blocking, so the horizon alone has to
///   keep them from being skipped -- which is what the tests need to see. On a locking read-committed server the
///   same reads wait for the writer instead, which is also correct. A scratch table with its own rowversion
///   lets a test hold the horizon open without touching the outbox, the long-running-transaction case.</item>
///   <item><see cref="UpgradeDatabase"/>, migrated to the migration before #927, given rows, then migrated
///   forward: the rows that exist when the column is added must get a value too.</item>
/// </list>
/// </summary>
public sealed class SqlServerFeedFixture : IAsyncLifetime
{
    /// <summary>The image docker-compose.yml pins for sql-server.</summary>
    public const string Image = "mcr.microsoft.com/mssql/server:2022-CU25-GDR2-ubuntu-22.04";

    /// <summary>The tenant migration immediately before #927's (#917's).</summary>
    public const string MigrationBeforeFeed = "20260923231643_Added_OutboxRetryWindow";

    public const string ScratchPinTable = "FeedTestHorizonPin";

    private readonly MsSqlContainer _container = new MsSqlBuilder(Image).Build();

    public string FeedDatabase { get; private set; } = null!;

    public string UpgradeDatabase { get; private set; } = null!;

    /// <summary>Ids of rows inserted into <see cref="UpgradeDatabase"/> BEFORE the column existed.</summary>
    public Guid[] RowsBeforeTheColumn { get; } = { Guid.NewGuid(), Guid.NewGuid() };

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        CaseEvaluationEfCoreEntityExtensionMappings.Configure();

        FeedDatabase = Catalog("CaseTrackerFeed");
        await using (var context = CreateContext(FeedDatabase))
        {
            await context.Database.MigrateAsync();
        }

        // From master, then drop pooled connections: switching the isolation mode needs the database to itself.
        await ExecuteAsync(Catalog("master"), "ALTER DATABASE [CaseTrackerFeed] SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;");
        SqlConnection.ClearAllPools();
        await ExecuteAsync(FeedDatabase, "CREATE TABLE [" + ScratchPinTable + "] ([Id] int IDENTITY PRIMARY KEY, [Version] rowversion NOT NULL);");

        UpgradeDatabase = Catalog("CaseTrackerFeedUpgrade");
        await using (var context = CreateContext(UpgradeDatabase))
        {
            await context.GetService<IMigrator>().MigrateAsync(MigrationBeforeFeed);
        }

        foreach (var id in RowsBeforeTheColumn)
        {
            await OutboxRows.InsertWithoutVersionAsync(UpgradeDatabase, id);
        }

        await using (var context = CreateContext(UpgradeDatabase))
        {
            await context.Database.MigrateAsync();
        }
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    /// <summary>A tenant context over <paramref name="connectionString"/>, built the way the design-time factory builds it.</summary>
    public static CaseEvaluationTenantDbContext CreateContext(string connectionString) =>
        new(new DbContextOptionsBuilder<CaseEvaluationTenantDbContext>().UseSqlServer(connectionString).Options);

    public static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private string Catalog(string name) =>
        new SqlConnectionStringBuilder(_container.GetConnectionString()) { InitialCatalog = name }.ConnectionString;
}
