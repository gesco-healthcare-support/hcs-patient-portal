using System.Collections.Generic;
using System.Diagnostics;

namespace HealthcareSupport.CaseEvaluation.Timing;

/// <summary>
/// Issue #1031 measurement only (a draft PR that never merges). Holds the Stopwatch timestamps
/// taken at each phase boundary of one integrated test, so <see cref="PhaseClock"/> can write
/// one row per test. Every boundary is a raw <see cref="Stopwatch.GetTimestamp"/> value; the
/// phases are derived when the row is written so that they sum to the total exactly.
/// </summary>
public sealed class PhaseRecord
{
    private readonly List<(string Contributor, long Ticks)> _contributors = new();

    internal PhaseRecord(int sequence, long started)
    {
        Sequence = sequence;
        Started = started;
    }

    public int Sequence { get; }

    public long Started { get; }

    public string TestClass { get; private set; } = string.Empty;

    public string StartupModule { get; private set; } = string.Empty;

    public long BeforeAdd { get; private set; }

    public long SchemaTicks { get; private set; }

    public long ProviderStart { get; private set; }

    public long ProviderEnd { get; private set; }

    public long SeedTicks { get; private set; }

    public bool SeedOpen { get; private set; }

    public long AfterInitialize { get; private set; }

    public long DisposeStart { get; private set; }

    public long DisposeEnd { get; private set; }

    public IReadOnlyList<(string Contributor, long Ticks)> Contributors => _contributors;

    public void MarkBeforeAdd(string testClass, string startupModule)
    {
        TestClass = testClass;
        StartupModule = startupModule;
        BeforeAdd = Stopwatch.GetTimestamp();
    }

    /// <summary>Adds the schema build (open, PRAGMA, CreateTables) measured inside ConfigureServices.</summary>
    public void AddSchema(long ticks) => SchemaTicks += ticks;

    public void MarkProviderStart() => ProviderStart = Stopwatch.GetTimestamp();

    public void MarkProviderEnd() => ProviderEnd = Stopwatch.GetTimestamp();

    /// <summary>
    /// Opens the seed window. Contributors are recorded only while it is open, so a seeder a test
    /// body triggers (a new tenant, say) is not counted as rig seeding.
    /// </summary>
    public void OpenSeed() => SeedOpen = true;

    /// <summary>Closes the seed window with the total measured at the call site, UoW completion included.</summary>
    public void CloseSeed(long ticks)
    {
        SeedTicks += ticks;
        SeedOpen = false;
    }

    public void AddContributor(string contributor, long ticks)
    {
        if (SeedOpen)
        {
            _contributors.Add((contributor, ticks));
        }
    }

    public void MarkAfterInitialize() => AfterInitialize = Stopwatch.GetTimestamp();

    public void MarkDisposeStart() => DisposeStart = Stopwatch.GetTimestamp();

    public void MarkDisposeEnd() => DisposeEnd = Stopwatch.GetTimestamp();
}
