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
    /// The date by which a request created at <paramref name="requestedAt"/>
    /// must be decided: the request date (time-of-day ignored) plus the window.
    /// </summary>
    internal static DateTime DecisionDueDate(DateTime requestedAt, int decisionDueDays)
    {
        return requestedAt.Date.AddDays(decisionDueDays);
    }

    /// <summary>
    /// True when the decision deadline has passed: <paramref name="today"/> is
    /// strictly after the due date. On the due date itself the request is still
    /// "due today", not overdue.
    /// </summary>
    internal static bool IsDecisionOverdue(DateTime requestedAt, DateTime today, int decisionDueDays)
    {
        return today.Date > DecisionDueDate(requestedAt, decisionDueDays);
    }

    /// <summary>
    /// The <c>CreationTime</c> cutoff for an EF query: a Pending request whose
    /// CreationTime is strictly before this instant (midnight of today minus the
    /// window) is overdue. Kept in lockstep with <see cref="IsDecisionOverdue"/>
    /// so the dashboard count equals the number of overdue rows in the digest.
    /// </summary>
    internal static DateTime OverdueCreationCutoff(DateTime today, int decisionDueDays)
    {
        return today.Date.AddDays(-decisionDueDays);
    }
}
