using System;
using HealthcareSupport.CaseEvaluation.Appointments;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// 2026-06-11 -- pure unit tests for <see cref="DecisionSlaPolicy"/>, the
/// shared decision-deadline math used by the daily pending digest and the
/// dashboard "decision overdue" tile. A Pending request must be decided
/// within the per-tenant window (PendingAppointmentOverDueNotificationDays,
/// default 3). On the due date itself the request is "due today", not yet
/// overdue; the day after the deadline it is overdue.
/// </summary>
public class DecisionSlaPolicyUnitTests
{
    private static readonly DateTime Today = new(2026, 6, 11);

    [Fact]
    public void DecisionDueDate_IsRequestedAtPlusWindow()
    {
        // Requested 2026-06-08, window 3 -> due 2026-06-11.
        DecisionSlaPolicy.DecisionDueDate(new DateTime(2026, 6, 8), 3)
            .ShouldBe(new DateTime(2026, 6, 11));
    }

    [Fact]
    public void DecisionDueDate_IgnoresTimeOfDay()
    {
        // The time component of RequestedAt must not shift the due date.
        DecisionSlaPolicy.DecisionDueDate(new DateTime(2026, 6, 8, 23, 45, 0), 3)
            .ShouldBe(new DateTime(2026, 6, 11));
    }

    [Fact]
    public void IsDecisionOverdue_OnDueDate_ReturnsFalse()
    {
        // Requested 3 days ago (2026-06-08) -> due today (2026-06-11):
        // still "due today", not overdue.
        DecisionSlaPolicy.IsDecisionOverdue(new DateTime(2026, 6, 8), Today, 3)
            .ShouldBeFalse();
    }

    [Fact]
    public void IsDecisionOverdue_DayAfterDueDate_ReturnsTrue()
    {
        // Requested 4 days ago (2026-06-07) -> due 2026-06-10, today is past it.
        DecisionSlaPolicy.IsDecisionOverdue(new DateTime(2026, 6, 7), Today, 3)
            .ShouldBeTrue();
    }

    [Fact]
    public void IsDecisionOverdue_RequestedToday_ReturnsFalse()
    {
        DecisionSlaPolicy.IsDecisionOverdue(Today, Today, 3).ShouldBeFalse();
    }

    [Fact]
    public void IsDecisionOverdue_IgnoresRequestedTimeOfDay()
    {
        // Requested late on 2026-06-07; due 2026-06-10; overdue on 2026-06-11
        // regardless of the request's time-of-day.
        DecisionSlaPolicy.IsDecisionOverdue(new DateTime(2026, 6, 7, 23, 59, 0), Today, 3)
            .ShouldBeTrue();
    }

    [Fact]
    public void OverdueCreationCutoff_IsTodayMinusWindow()
    {
        // window 3, today 2026-06-11 -> cutoff 2026-06-08 (midnight).
        DecisionSlaPolicy.OverdueCreationCutoff(Today, 3)
            .ShouldBe(new DateTime(2026, 6, 8));
    }

    [Fact]
    public void OverdueCreationCutoff_AgreesWithIsDecisionOverdue()
    {
        // A request whose CreationTime is strictly before the cutoff is
        // overdue; one at-or-after is not. This keeps the dashboard DB query
        // (CreationTime < cutoff) in lockstep with the per-row predicate.
        var cutoff = DecisionSlaPolicy.OverdueCreationCutoff(Today, 3);

        var justBefore = cutoff.AddSeconds(-1);
        var atCutoff = cutoff;

        DecisionSlaPolicy.IsDecisionOverdue(justBefore, Today, 3).ShouldBeTrue();
        DecisionSlaPolicy.IsDecisionOverdue(atCutoff, Today, 3).ShouldBeFalse();
    }
}
