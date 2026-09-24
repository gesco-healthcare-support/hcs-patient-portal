using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace HealthcareSupport.CaseEvaluation.Timing;

/// <summary>
/// Issue #1031 measurement only (a draft PR that never merges). Starts one
/// <see cref="PhaseRecord"/> per integrated test and, when the test is disposed, appends one row to
/// <c>phase-timings-{pid}.csv</c> and one row per seed contributor to
/// <c>seed-contributors-{pid}.csv</c>.
/// </summary>
/// <remarks>
/// The files go to <c>test-results/</c> at the repository root, which CI already uploads as the
/// <c>backend-test-results</c> artifact, so <c>ci.yml</c> is not touched. <c>EF_PHASE_TIMINGS_DIR</c>
/// overrides the directory for local runs. The process id is in the name because every test
/// assembly runs in its own testhost at the same time; one writer per file keeps rows whole.
/// </remarks>
public static class PhaseClock
{
    private const string DirectoryVariable = "EF_PHASE_TIMINGS_DIR";
    private const string SolutionFile = "HealthcareSupport.CaseEvaluation.slnx";

    private static readonly AsyncLocal<PhaseRecord?> CurrentRecord = new();
    private static readonly object WriteLock = new();
    private static readonly Lazy<string?> OutputDirectory = new(ResolveOutputDirectory);
    private static int _sequence;
    private static bool _headersWritten;

    /// <summary>The record of the test whose constructor, body or Dispose is running, if any.</summary>
    public static PhaseRecord? Current => CurrentRecord.Value;

    /// <summary>Starts a record. Called from a field initializer, which runs BEFORE the base constructor.</summary>
    public static PhaseRecord Start()
    {
        var record = new PhaseRecord(Interlocked.Increment(ref _sequence), Stopwatch.GetTimestamp());
        CurrentRecord.Value = record;
        return record;
    }

    /// <summary>Writes the record's rows and clears it as the current record.</summary>
    public static void Finish(PhaseRecord record)
    {
        CurrentRecord.Value = null;
        var directory = OutputDirectory.Value;
        if (directory == null)
        {
            return;
        }

        var pid = Environment.ProcessId.ToString(CultureInfo.InvariantCulture);
        var phasesPath = Path.Combine(directory, $"phase-timings-{pid}.csv");
        var contributorsPath = Path.Combine(directory, $"seed-contributors-{pid}.csv");

        var schema = record.SchemaTicks;
        var seed = record.SeedTicks;
        var pre = record.BeforeAdd - record.Started;
        var configure = record.ProviderStart - record.BeforeAdd - schema;
        var container = record.ProviderEnd - record.ProviderStart;
        var initOther = record.AfterInitialize - record.ProviderEnd - seed;
        var body = record.DisposeStart - record.AfterInitialize;
        var shutdown = record.DisposeEnd - record.DisposeStart;
        var total = record.DisposeEnd - record.Started;
        var contributorSum = record.Contributors.Sum(c => c.Ticks);

        var phases = string.Join(",", new[]
        {
            record.Sequence.ToString(CultureInfo.InvariantCulture), pid, record.StartupModule, record.TestClass,
            Ms(pre), Ms(configure), Ms(schema), Ms(container), Ms(initOther), Ms(seed),
            Ms(body), Ms(shutdown), Ms(total), Ms(contributorSum),
        });

        var contributors = new StringBuilder();
        foreach (var (name, ticks) in record.Contributors)
        {
            contributors.Append(record.Sequence.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(pid).Append(',').Append(name).Append(',').Append(Ms(ticks)).Append('\n');
        }

        lock (WriteLock)
        {
            if (!_headersWritten)
            {
                File.AppendAllText(phasesPath,
                    "seq,pid,startup_module,test_class,pre_ms,configure_ms,schema_ms,container_ms," +
                    "init_other_ms,seed_ms,body_ms,shutdown_ms,total_ms,contributor_sum_ms\n");
                File.AppendAllText(contributorsPath, "seq,pid,contributor,ms\n");
                _headersWritten = true;
            }

            File.AppendAllText(phasesPath, phases + "\n");
            File.AppendAllText(contributorsPath, contributors.ToString());
        }
    }

    private static string Ms(long ticks) =>
        (ticks * 1000.0 / Stopwatch.Frequency).ToString("F3", CultureInfo.InvariantCulture);

    private static string? ResolveOutputDirectory()
    {
        var overridden = Environment.GetEnvironmentVariable(DirectoryVariable);
        if (!string.IsNullOrWhiteSpace(overridden))
        {
            Directory.CreateDirectory(overridden);
            return overridden;
        }

        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, SolutionFile)))
            {
                var results = Path.Combine(dir.FullName, "test-results");
                Directory.CreateDirectory(results);
                return results;
            }
        }

        return null;
    }
}
