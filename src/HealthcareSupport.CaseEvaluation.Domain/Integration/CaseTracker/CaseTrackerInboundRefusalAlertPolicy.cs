using System;
using System.Collections.Concurrent;
using HealthcareSupport.CaseEvaluation.Enums;
using Volo.Abp.DependencyInjection;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Decides whether a refused INBOUND attendance report should raise an email, so a Case Tracker retrying every
/// minute produces one alert rather than sixty an hour (#1043).
///
/// <para><b>Why this is in memory rather than a table.</b> The gap #1043 describes is that nobody is TOLD, not
/// that nobody can query the history afterwards: there is no screen for refused inbound reports and nothing
/// asks for one. A column on every office database, migrated everywhere, is a real cost for a record with no
/// reader. <see cref="Volo.Abp.DependencyInjection.ISingletonDependency"/> state with a documented
/// per-instance caveat is the shape this codebase already uses for exactly this kind of bookkeeping -- see
/// <c>CaseTrackerFeedAllowance</c>, which counts an office's hourly feed budget the same way.</para>
///
/// <para><b>What that costs, stated plainly.</b> The suppression is per API instance, so two instances could
/// each send one email for the same incident, and a restart re-arms every suppression. Both fail towards
/// alerting rather than towards silence, which is the right direction for a mechanism whose entire purpose is
/// to stop a failure going unnoticed. If the API is ever scaled out, this moves to a shared store alongside
/// <c>CaseTrackerFeedAllowance</c>, which has the same constraint for the same reason.</para>
///
/// <para><b>Re-arming mirrors the cursor-ahead alert</b> (#927): further refusals are logged but not emailed
/// until a report for that appointment succeeds. Keyed per appointment rather than per office, so one stuck
/// appointment cannot mask a second one starting to fail.</para>
/// </summary>
public class CaseTrackerInboundRefusalAlertPolicy : ISingletonDependency
{
    /// <summary>
    /// How long a suppression survives without being cleared by a success. A bound rather than a schedule:
    /// without it, an office whose reports never succeed would hold its entries for the process lifetime. A
    /// day is long enough that a retrying consumer stays quiet through a weekend outage, and short enough that
    /// a genuinely new incident is not silently folded into a stale one.
    /// </summary>
    public static readonly TimeSpan SuppressionWindow = TimeSpan.FromHours(24);

    private readonly ConcurrentDictionary<(Guid OfficeId, Guid AppointmentId, CaseTrackerInboundRefusalReason Reason), DateTime> _alerted = new();

    /// <summary>
    /// True when this refusal should raise an email. The first call for a key returns true and records it;
    /// later calls return false until <see cref="ClearAsync"/> or the window lapses.
    /// </summary>
    public virtual bool ShouldAlert(
        Guid officeId,
        Guid appointmentId,
        CaseTrackerInboundRefusalReason reason,
        DateTime nowUtc)
    {
        Prune(nowUtc);

        var key = (officeId, appointmentId, reason);
        var added = true;

        _alerted.AddOrUpdate(
            key,
            _ => nowUtc,
            (_, existing) =>
            {
                // Expired entries are treated as absent, so a long-running incident re-alerts once a day
                // rather than falling permanently silent.
                if (nowUtc - existing >= SuppressionWindow)
                {
                    return nowUtc;
                }

                added = false;
                return existing;
            });

        return added;
    }

    /// <summary>
    /// Re-arms the alert for an appointment once a report for it has been applied. Called on the success path
    /// so the NEXT failure is heard, exactly as a good feed request re-arms the cursor-ahead alert.
    /// </summary>
    public virtual void Clear(Guid officeId, Guid appointmentId)
    {
        foreach (var reason in Enum.GetValues<CaseTrackerInboundRefusalReason>())
        {
            _alerted.TryRemove((officeId, appointmentId, reason), out _);
        }
    }

    /// <summary>Drops lapsed entries so a process that runs for months does not accumulate them.</summary>
    private void Prune(DateTime nowUtc)
    {
        foreach (var entry in _alerted)
        {
            if (nowUtc - entry.Value >= SuppressionWindow)
            {
                _alerted.TryRemove(entry.Key, out _);
            }
        }
    }
}
