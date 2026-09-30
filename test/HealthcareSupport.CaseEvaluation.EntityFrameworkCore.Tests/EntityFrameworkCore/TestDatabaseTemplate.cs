using System;
using System.Threading;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore;

/// <summary>
/// The once-per-process template database (#1033): tables created and the full seed run exactly once,
/// then copied into each test application's own in-memory database.
/// </summary>
/// <remarks>
/// <para>
/// Before this, every test application created every table and ran the full <c>IDataSeeder</c>, about
/// three quarters of each test's time (CI run 36032212623: seed 70.8%, CreateTables 4.1%).
/// </para>
/// <para>
/// THREAD SAFETY, for test classes that run in parallel (#1034). The template is built under
/// <see cref="LazyThreadSafetyMode.ExecutionAndPublication"/>, so exactly one caller builds it and the
/// rest wait. A build that throws is cached, so every later test fails with the same error rather than
/// silently falling back. The copy runs under a lock because one <see cref="SqliteConnection"/> is not
/// safe to use from two threads at once.
/// </para>
/// </remarks>
public static class TestDatabaseTemplate
{
    /// <summary>Set this environment variable to <c>1</c> to make every test build its database fresh.</summary>
    public const string FreshBuildsEnvironmentVariable = "CASEEVAL_TEST_DB_FRESH";

    private static readonly Lazy<SqliteConnection> Template =
        new(BuildTemplate, LazyThreadSafetyMode.ExecutionAndPublication);

    private static readonly object CopyLock = new();

    /// <summary>
    /// True when <see cref="FreshBuildsEnvironmentVariable"/> is <c>1</c>: the switch for timing both
    /// modes on one commit, or for ruling the template in or out when a test fails.
    /// </summary>
    public static bool FreshBuildsRequested() =>
        Environment.GetEnvironmentVariable(FreshBuildsEnvironmentVariable) == "1";

    /// <summary>
    /// Opens a new in-memory database holding a copy of the template. The caller owns the connection.
    /// </summary>
    public static SqliteConnection CreateCopy()
    {
        var template = Template.Value;

        // Opened BEFORE the copy, with foreign keys on: BackupDatabase would open and then close a closed
        // destination, and closing a :memory: connection discards its database.
        var connection = CaseEvaluationEntityFrameworkCoreTestModule.OpenConnection();
        try
        {
            lock (CopyLock)
            {
                template.BackupDatabase(connection);
            }

            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    private static SqliteConnection BuildTemplate()
    {
        using var freshApplication = new FreshTestDatabaseApplication();

        var template = new SqliteConnection("Data Source=:memory:");
        template.Open();
        freshApplication.Database.Connection.BackupDatabase(template);
        return template;
    }
}

/// <summary>
/// A test application that builds its database fresh (tables created, full seed run) through exactly
/// the construction path every test uses. It builds the template, and the equivalence test uses it as
/// the reference.
/// </summary>
public sealed class FreshTestDatabaseApplication
    : CaseEvaluationTestBase<CaseEvaluationEntityFrameworkCoreTestModule>
{
    /// <summary>This application's freshly built database.</summary>
    public CaseEvaluationTestDatabase Database => GetRequiredService<CaseEvaluationTestDatabase>();

    protected override void BeforeAddApplication(IServiceCollection services)
    {
        base.BeforeAddApplication(services);
        services.PreConfigure<CaseEvaluationTestDatabaseOptions>(options => options.BuildFresh = true);
    }
}
