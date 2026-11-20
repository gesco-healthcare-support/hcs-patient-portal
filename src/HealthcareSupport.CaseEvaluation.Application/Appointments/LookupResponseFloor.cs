using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// 2026-11-20 -- a constant minimum response time for the two lookups that must not say what they found:
/// the re-evaluation source lookup by confirmation number and date of birth (<see cref="ReEvaluationSourcesAppService"/>)
/// and the "I don't know the number" request (<see cref="ConfirmationNumberLookupsAppService"/>).
///
/// <para><b>Why.</b> Their answers are identical by design, but the work behind them is not. An unknown
/// confirmation number costs one query, while a known one with the wrong date of birth also walks the
/// re-evaluation chain and reads the patient. A matched number request sends two emails before its 204. The
/// time taken could therefore tell a caller what the answer hides. Measured on the live server 2026-11-20 (Sandbox,
/// server side): refusals 36-44 ms, matched requests about 70 ms, unmatched requests about 106 ms. The floor sits
/// well above all of them.</para>
///
/// <para><b>How.</b> Callers take a <see cref="Start"/> stamp first and call <see cref="PadAsync"/> in a
/// <c>finally</c>, so a path that throws (the hourly throttle, for one) waits too. An answer the caller is
/// entitled to (a case it may load) is not padded: it reveals the match anyway.</para>
/// </summary>
public class LookupResponseFloor : ISingletonDependency
{
    /// <summary>The minimum time every padded answer takes.</summary>
    public static readonly TimeSpan Floor = TimeSpan.FromMilliseconds(750);

    /// <summary>A timestamp to measure the answer from; take it before any work.</summary>
    public virtual long Start() => Stopwatch.GetTimestamp();

    /// <summary>Waits until <see cref="Floor"/> has passed since <paramref name="startTimestamp"/>.</summary>
    public virtual Task PadAsync(long startTimestamp)
    {
        var remaining = Remaining(Stopwatch.GetElapsedTime(startTimestamp));
        return remaining > TimeSpan.Zero ? Task.Delay(remaining) : Task.CompletedTask;
    }

    /// <summary>How much longer an answer that has taken <paramref name="elapsed"/> must wait; never negative.</summary>
    public static TimeSpan Remaining(TimeSpan elapsed) => elapsed >= Floor ? TimeSpan.Zero : Floor - elapsed;
}
