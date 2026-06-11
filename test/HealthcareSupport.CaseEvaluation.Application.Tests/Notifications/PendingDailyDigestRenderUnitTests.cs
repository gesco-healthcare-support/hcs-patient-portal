using System;
using System.Collections.Generic;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using HealthcareSupport.CaseEvaluation.Notifications.Handlers;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Notifications;

/// <summary>
/// 2026-06-11 -- pure render tests for
/// <see cref="PendingDailyDigestEmailHandler.BuildDigestHtml"/>. Verifies the
/// decision-SLA wiring: the "Decision due" column = RequestedAt + the supplied
/// window (driven by the per-tenant setting, not a hardcoded 5), and rows past
/// the deadline are highlighted + flagged OVERDUE with an escalation banner.
/// </summary>
public class PendingDailyDigestRenderUnitTests
{
    private static readonly DateTime Now = new(2026, 6, 11, 9, 0, 0, DateTimeKind.Utc);

    private static PendingDailyDigestRow Row(DateTime requestedAt) => new()
    {
        RequestConfirmationNumber = "A00042",
        PatientName = "Test Patient",
        AppointmentDate = new DateTime(2026, 7, 1),
        DueDate = null,
        RequestedAt = requestedAt,
    };

    [Fact]
    public void BuildDigestHtml_DecisionDueColumn_UsesSuppliedWindowNotHardcodedFive()
    {
        // window = 3, requested 2026-06-10 -> decision due 2026-06-13 (NOT +5).
        var html = PendingDailyDigestEmailHandler.BuildDigestHtml(
            new List<PendingDailyDigestRow> { Row(new DateTime(2026, 6, 10)) },
            decisionDueDays: 3,
            nowForOverdue: Now);

        html.ShouldContain("06/13/2026");
        html.ShouldNotContain("06/15/2026"); // the old +5 behavior
    }

    [Fact]
    public void BuildDigestHtml_OverdueRow_IsFlaggedAndBannerShown()
    {
        // requested 2026-06-06 -> due 2026-06-09; today 2026-06-11 -> overdue.
        var html = PendingDailyDigestEmailHandler.BuildDigestHtml(
            new List<PendingDailyDigestRow> { Row(new DateTime(2026, 6, 6)) },
            decisionDueDays: 3,
            nowForOverdue: Now);

        html.ShouldContain("(OVERDUE)");
        html.ShouldContain("past the 3-day decision deadline");
    }

    [Fact]
    public void BuildDigestHtml_OnDueDate_NotOverdue()
    {
        // requested 2026-06-08 -> due 2026-06-11 = today: due today, not overdue.
        var html = PendingDailyDigestEmailHandler.BuildDigestHtml(
            new List<PendingDailyDigestRow> { Row(new DateTime(2026, 6, 8)) },
            decisionDueDays: 3,
            nowForOverdue: Now);

        html.ShouldNotContain("(OVERDUE)");
        html.ShouldNotContain("decision deadline");
    }

    [Fact]
    public void BuildDigestHtml_BannerCountsOnlyOverdueRows()
    {
        var html = PendingDailyDigestEmailHandler.BuildDigestHtml(
            new List<PendingDailyDigestRow>
            {
                Row(new DateTime(2026, 6, 6)),  // overdue
                Row(new DateTime(2026, 6, 7)),  // overdue (due 06-10)
                Row(new DateTime(2026, 6, 10)), // not overdue (due 06-13)
            },
            decisionDueDays: 3,
            nowForOverdue: Now);

        html.ShouldContain("2 pending requests are past");
    }
}
