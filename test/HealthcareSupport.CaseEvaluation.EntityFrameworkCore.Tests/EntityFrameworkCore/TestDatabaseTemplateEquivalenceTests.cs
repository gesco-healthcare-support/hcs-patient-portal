using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using Shouldly;
using Volo.Abp.SettingManagement;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore;

/// <summary>
/// Proves the database a test receives as a copy of the once-per-process template (#1033) is the
/// database a fresh build would have given it: the same schema and the same number of rows in every
/// table, with foreign keys enforced.
/// </summary>
/// <remarks>
/// <para>
/// This is the harness the office-isolation tests stand on, so a copy that differed from a fresh build
/// could let one of them pass for the wrong reason.
/// </para>
/// <para>
/// WHAT IT CANNOT PROVE: value-level equality. Random ids, concurrency stamps and audit times
/// legitimately differ between two builds, so only the schema and the row counts are compared. Nor can
/// it see a race between background start-up work and the test body, which is timing rather than data;
/// the one found while building this (the settings store) has its own guard below.
/// </para>
/// <para>
/// No <c>[Collection]</c>: this class shares no state with other tests. It builds one extra fresh
/// application of its own, and the template is safe to copy from in parallel.
/// </para>
/// </remarks>
public class TestDatabaseTemplateEquivalenceTests : CaseEvaluationEntityFrameworkCoreTestBase
{
    [Fact]
    public void A_copied_database_has_the_schema_and_row_counts_of_a_freshly_built_one()
    {
        var copy = RequireTemplateCopy();

        using var freshApplication = new FreshTestDatabaseApplication();
        var fresh = freshApplication.Database;
        fresh.FromTemplate.ShouldBeFalse("the reference database must be built fresh, not copied");

        var freshCounts = RowCountsByTable(fresh.Connection);
        freshCounts.Values.Sum().ShouldBeGreaterThan(0,
            "a fresh build with no rows means the seed did not run, and an empty copy would then match it");

        SchemaRows(copy.Connection).ShouldBe(SchemaRows(fresh.Connection));

        var copyCounts = RowCountsByTable(copy.Connection);
        var differences = freshCounts.Keys.Union(copyCounts.Keys)
            .Where(table => freshCounts.GetValueOrDefault(table, -1) != copyCounts.GetValueOrDefault(table, -1))
            .Select(table => $"{table}: fresh {freshCounts.GetValueOrDefault(table, -1)}, copy {copyCounts.GetValueOrDefault(table, -1)}")
            .ToList();
        differences.ShouldBeEmpty("every table must hold as many rows in the copy as in a fresh build");
    }

    [Fact]
    public void A_copied_database_enforces_foreign_keys()
    {
        var copy = RequireTemplateCopy();

        ScalarLong(copy.Connection, "PRAGMA foreign_keys;").ShouldBe(1,
            "foreign-key enforcement belongs to the connection, not the copied pages, so the copy path must switch it on");
    }

    [Fact]
    public void The_settings_store_does_not_write_to_the_database_in_the_background()
    {
        // ABP's setting management saves the static setting definitions from a background task that
        // starts during application start-up. The TestBase seed used to run for about a second after
        // that, which hid the task. With the seed skipped on a copy, the test body raced the task on the
        // same SQLite connection and failed at random with "database is locked" or "unable to
        // delete/modify user-function due to active statements". The features, permissions and text
        // templates stores are switched off for the same reason.
        var options = GetRequiredService<IOptions<SettingManagementOptions>>().Value;

        options.SaveStaticSettingsToDatabase.ShouldBeFalse();
        options.IsDynamicSettingStoreEnabled.ShouldBeFalse();
    }

    /// <summary>
    /// This test's own database, which must be a template copy: comparing a fresh build against another
    /// fresh build would prove nothing.
    /// </summary>
    private CaseEvaluationTestDatabase RequireTemplateCopy()
    {
        var database = GetRequiredService<CaseEvaluationTestDatabase>();
        database.FromTemplate.ShouldBeTrue(
            $"this test's database must be a template copy; is {TestDatabaseTemplate.FreshBuildsEnvironmentVariable}=1 set?");
        return database;
    }

    private static List<string> SchemaRows(SqliteConnection connection)
    {
        var rows = new List<string>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT type, name, tbl_name, sql FROM sqlite_master ORDER BY type, name;";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(string.Join(" | ", Enumerable.Range(0, 4).Select(i => reader.IsDBNull(i) ? "<null>" : reader.GetString(i))));
        }

        return rows;
    }

    private static Dictionary<string, long> RowCountsByTable(SqliteConnection connection)
    {
        var tables = new List<string>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' ORDER BY name;";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                tables.Add(reader.GetString(0));
            }
        }

        return tables.ToDictionary(
            table => table,
            table => ScalarLong(connection, $"SELECT COUNT(*) FROM \"{table.Replace("\"", "\"\"")}\";"));
    }

    private static long ScalarLong(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (long)command.ExecuteScalar()!;
    }
}
