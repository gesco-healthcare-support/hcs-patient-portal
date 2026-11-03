using System;
using System.IO;
using System.Linq;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using HealthcareSupport.CaseEvaluation.Notifications.Jobs;
using HealthcareSupport.CaseEvaluation.Timing;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Notifications;

/// <summary>
/// The twice-daily summaries that replace the two daily digests: the schedule (08:00 and 16:00 Pacific),
/// the retired Hangfire ids, the rendered content and the run label. The wiring tests read the host module
/// source because a change there is uncoverable by the changed-lines floor and nothing else would notice a
/// regression in it.
/// </summary>
public class StaffSummaryTests
{
    [Fact]
    public void Cron_fires_at_minute_zero_of_hours_eight_and_sixteen_every_day()
    {
        // Hangfire's cron parser is not public, so the five fields are read directly: minute, hour, day of
        // month, month, day of week. The time zone is pinned separately, by the host-module wiring test below.
        var fields = StaffSummaryJob.CronExpression.Split(' ');
        fields.ShouldBe(new[] { "0", "8,16", "*", "*", "*" });
    }

    [Fact]
    public void Eight_and_sixteen_Pacific_are_the_morning_and_afternoon_runs_in_summer_and_winter()
    {
        // 08:00 PDT = 15:00 UTC and 16:00 PDT = 23:00 UTC; in standard time they are 16:00 and 00:00 UTC.
        StaffSummaryHtml.LabelFor(new DateTime(2026, 10, 23, 15, 0, 0, DateTimeKind.Utc)).ShouldBe("Morning");
        StaffSummaryHtml.LabelFor(new DateTime(2026, 10, 23, 23, 0, 0, DateTimeKind.Utc)).ShouldBe("Afternoon");
        StaffSummaryHtml.LabelFor(new DateTime(2026, 12, 1, 16, 0, 0, DateTimeKind.Utc)).ShouldBe("Morning");
        StaffSummaryHtml.LabelFor(new DateTime(2026, 12, 2, 0, 0, 0, DateTimeKind.Utc)).ShouldBe("Afternoon");
        PacificTime.FromUtc(new DateTime(2026, 10, 23, 15, 0, 0, DateTimeKind.Utc)).Hour.ShouldBe(8);
        PacificTime.FromUtc(new DateTime(2026, 12, 1, 16, 0, 0, DateTimeKind.Utc)).Hour.ShouldBe(8);
    }

    [Fact]
    public void The_two_old_digest_ids_are_the_ones_purged()
    {
        RetiredRecurringJobs.Ids.ShouldBe(
            new[] { "appt-pending-daily-digest", "appt-internal-staff-queue-digest" }, ignoreOrder: true);
        RetiredRecurringJobs.Ids.ShouldNotContain(StaffSummaryJob.RecurringJobId);
    }

    [Fact]
    public void The_host_module_purges_the_retired_ids_and_registers_the_summary_in_Pacific_time()
    {
        var module = File.ReadAllText(Path.Combine(
            RepoRoot(), "src", "HealthcareSupport.CaseEvaluation.HttpApi.Host", "CaseEvaluationHttpApiHostModule.cs"));

        module.ShouldContain("var options = new RecurringJobOptions { TimeZone = PacificTime.Zone };");
        module.ShouldContain("foreach (var retiredId in HealthcareSupport.CaseEvaluation.Notifications.Jobs.RetiredRecurringJobs.Ids)");
        module.ShouldContain("global::Hangfire.RecurringJob.RemoveIfExists(retiredId);");
        module.ShouldContain("AddOrUpdate<HealthcareSupport.CaseEvaluation.Notifications.Jobs.StaffSummaryJob>(");
        module.ShouldContain("StaffSummaryJob.CronExpression,");
        module.ShouldNotContain("PendingDailyDigestJob");
        module.ShouldNotContain("InternalStaffQueueDigestJob");
    }

    [Fact]
    public void The_office_summary_lists_counts_and_pending_rows_and_flags_overdue()
    {
        var html = StaffSummaryHtml.BuildOffice(new OfficeSummaryEto
        {
            DecisionDueDays = 3,
            Counts = new StaffSummaryCounts { Pending = 2, PendingOverdue = 1, Approved = 5, InfoRequested = 1 },
            Rows =
            {
                new OfficeSummaryRow
                {
                    RequestConfirmationNumber = "TEST-A1",
                    PatientName = "TEST-<Pat>",
                    AppointmentDate = new DateTime(2026, 11, 20),
                    DecisionDue = new DateTime(2026, 11, 2),
                    IsOverdue = true,
                },
                new OfficeSummaryRow
                {
                    RequestConfirmationNumber = "TEST-A2",
                    PatientName = "TEST-Quiet",
                    AppointmentDate = new DateTime(2026, 11, 25),
                    DueDate = new DateTime(2026, 11, 28),
                    DecisionDue = new DateTime(2026, 11, 9),
                },
            },
        });

        html.ShouldContain("1 pending request is past the 3-day decision deadline");
        html.ShouldContain("TEST-A1");
        html.ShouldContain("(OVERDUE)");
        html.ShouldContain("TEST-&lt;Pat&gt;");
        html.ShouldNotContain("<Pat>");
        html.ShouldContain("11/28/2026");
        html.ShouldContain("Approved appointments");
        html.IndexOf("(OVERDUE)", StringComparison.Ordinal).ShouldBe(html.LastIndexOf("(OVERDUE)", StringComparison.Ordinal));
    }

    [Fact]
    public void The_supervisor_summary_has_one_row_per_office_and_a_total()
    {
        var html = StaffSummaryHtml.BuildSupervisor(new SupervisorSummaryEto
        {
            Offices =
            {
                new SupervisorSummaryOffice { OfficeName = "test-a", Counts = new StaffSummaryCounts { Pending = 2, PendingOverdue = 1 } },
                new SupervisorSummaryOffice { OfficeName = "test-<b>", Counts = new StaffSummaryCounts { Pending = 3, CancellationRequested = 4 } },
            },
        });

        html.ShouldContain("test-a");
        html.ShouldContain("test-&lt;b&gt;");
        html.ShouldContain("All offices");
        html.ShouldContain(">5</td>"); // pending total
        html.ShouldContain(">4</td>"); // cancellation total
    }

    [Fact]
    public void Approved_alone_is_not_outstanding()
    {
        new StaffSummaryCounts { Approved = 9 }.HasOutstanding.ShouldBeFalse();
        new StaffSummaryCounts { Pending = 1 }.HasOutstanding.ShouldBeTrue();
        new StaffSummaryCounts { RescheduleRequested = 1 }.HasOutstanding.ShouldBeTrue();
        new StaffSummaryCounts { CancellationRequested = 1 }.HasOutstanding.ShouldBeTrue();
        new StaffSummaryCounts { InfoRequested = 1 }.HasOutstanding.ShouldBeTrue();
    }

    private static string RepoRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d != null && !d.GetFiles("*.slnx").Any())
        {
            d = d.Parent;
        }
        return d?.FullName ?? throw new InvalidOperationException("repo root not found");
    }
}
