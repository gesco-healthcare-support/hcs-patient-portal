using Xunit;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker.SqlServer;

/// <summary>
/// ONE SQL Server container for every test class that needs a real one.
///
/// <para>A class fixture is instantiated per class, so two classes each declaring
/// <c>IClassFixture&lt;SqlServerFeedFixture&gt;</c> start two containers, and xUnit runs the classes
/// in parallel, so both exist at once. A SQL Server container wants roughly 2 GB; on a developer
/// box also running the application stack, or on a CI runner, the second one loses its race with
/// the Docker daemon and every test in that class fails with a TaskCanceledException that looks
/// nothing like its cause.</para>
///
/// <para>A COLLECTION fixture is created once for all classes in the collection, so there is one
/// container, and the classes in it run sequentially rather than competing. Any future class
/// needing real SQL Server should join this collection rather than declare its own fixture.</para>
/// </summary>
[CollectionDefinition(Name)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFeedFixture>
{
    public const string Name = "sql-server";
}
