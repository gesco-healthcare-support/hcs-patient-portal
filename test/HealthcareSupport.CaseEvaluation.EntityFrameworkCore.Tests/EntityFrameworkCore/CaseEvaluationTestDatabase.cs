using Microsoft.Data.Sqlite;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore;

/// <summary>
/// The in-memory SQLite database one test application runs against, registered as a singleton so
/// tests can inspect it directly.
/// </summary>
public class CaseEvaluationTestDatabase
{
    public CaseEvaluationTestDatabase(SqliteConnection connection, bool fromTemplate)
    {
        Connection = connection;
        FromTemplate = fromTemplate;
    }

    /// <summary>The open connection that holds the database. Owned by the test module; never dispose it.</summary>
    public SqliteConnection Connection { get; }

    /// <summary>
    /// True when the database was copied from the once-per-process template; false when it was built
    /// fresh (tables created, then the full seed run).
    /// </summary>
    public bool FromTemplate { get; }
}

/// <summary>
/// How <see cref="CaseEvaluationEntityFrameworkCoreTestModule"/> builds a test application's database.
/// Set with <c>services.PreConfigure&lt;CaseEvaluationTestDatabaseOptions&gt;(...)</c> before the
/// application is added.
/// </summary>
public class CaseEvaluationTestDatabaseOptions
{
    /// <summary>
    /// When true, the application creates the tables and runs the full seed itself instead of copying
    /// the template. The template builder and the equivalence test use it.
    /// </summary>
    public bool BuildFresh { get; set; }
}
