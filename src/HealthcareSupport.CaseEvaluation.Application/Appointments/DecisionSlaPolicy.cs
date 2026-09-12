using HealthcareSupport.CaseEvaluation.Timing;
using System;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// 2026-06-11 -- pure decision-SLA helpers. Staff legally have 5 days to
/// approve or reject a Pending request; the practice keeps a tighter
/// per-tenant window for safety margin
/// (<c>SystemParameter.PendingAppointmentOverDueNotificationDays</c>,
/// default 3). These helpers centralize the "decision due" / "overdue" math
/// so the daily pending digest (<c>PendingDailyDigestEmailHandler</c>) and the
/// dashboard "decision overdue" tile (<c>DashboardAppService</c>) agree to the
/// day. No status change is ever made -- the deadline only escalates / notifies.
///
/// Extracted as <c>internal static</c> for unit-testability via the existing
/// <c>InternalsVisibleTo</c> wiring (matches the Phase 11a/11b validator pattern).
/// </summary>
internal static class DecisionSlaPolicy
{
    /// <summary>
    /// The PACIFIC date by which a request created at <paramref name="requestedAtUtc"/> must be
    /// decided: the request's Pacific calendar date plus the window.
    ///
    /// <para>2026-08-31: this took <c>.Date</c> off the raw UTC instant, so a request created after
    /// 5pm Pacific was credited to the NEXT day and its deadline printed one day late in the
    /// pending digest email. Every parameter here is now named for the kind of value it holds and
    /// converted inside, so a caller cannot pass a UTC date by mistake -- which is exactly how the
    /// original bug survived review.</para>
    /// </summary>
    internal static DateTime DecisionDueDate(DateTime requestedAtUtc, int decisionDueDays)
    {
        return PacificTime.TodayFrom(requestedAtUtc).AddDays(decisionDueDays);
    }

    /// <summary>
    /// True when the decision deadline has passed: today in PACIFIC time is strictly after the due
    /// date. On the due date itself the request is still "due today", not overdue.
    ///
    /// <para>Takes the UTC instant rather than a date so the Pacific conversion cannot be skipped
    /// by a caller. This drives the red "overdue" row in the staff digest, so an off-by-one here
    /// tells someone a request is late when it is not, or the reverse.</para>
    /// </summary>
    internal static bool IsDecisionOverdue(DateTime requestedAtUtc, DateTime nowUtc, int decisionDueDays)
    {
        return PacificTime.TodayFrom(nowUtc) > DecisionDueDate(requestedAtUtc, decisionDueDays);
    }

    /// <summary>
    /// The <c>CreationTime</c> cutoff for an EF query, as a UTC INSTANT: a Pending request whose
    /// CreationTime is strictly before this instant is overdue. Kept in lockstep with
    /// <see cref="IsDecisionOverdue"/> so the dashboard count equals the number of overdue rows in
    /// the digest.
    ///
    /// <para>The return value MUST stay UTC. It is compared against <c>CreationTime</c> inside SQL,
    /// where the stored values are UTC, so handing back a Pacific wall-clock value would skew the
    /// comparison by 7 or 8 hours -- a silently wrong COUNT, which is harder to notice than a
    /// wrong printed date. Hence <see cref="PacificTime.StartOfDayUtc"/>: the boundary is Pacific
    /// midnight, expressed as the UTC instant that midnight actually occurred at.</para>
    ///
    /// <para>Lockstep proof: <see cref="IsDecisionOverdue"/> is true when
    /// <c>pacificToday &gt; requestPacificDate + n</c>, i.e. when
    /// <c>requestPacificDate &lt; pacificToday - n</c>. Comparing <c>CreationTime</c> against the
    /// UTC instant of Pacific midnight on <c>pacificToday - n</c> selects exactly those rows.</para>
    /// </summary>
    internal static DateTime OverdueCreationCutoff(DateTime nowUtc, int decisionDueDays)
    {
        var boundary = PacificTime.TodayFrom(nowUtc).AddDays(-decisionDueDays);
        return PacificTime.StartOfDayUtc(boundary);
    }
}
