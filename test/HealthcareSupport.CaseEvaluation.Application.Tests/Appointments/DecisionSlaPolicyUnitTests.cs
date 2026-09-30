using HealthcareSupport.CaseEvaluation.Appointments;
using Shouldly;
using System;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// Pure unit tests for <see cref="DecisionSlaPolicy"/>, the shared decision-deadline math used by
/// the daily pending digest and the dashboard "decision overdue" tile. A Pending request must be
/// decided within the per-tenant window (PendingAppointmentOverDueNotificationDays, default 3). On
/// the due date itself the request is "due today", not yet overdue; the day after, it is overdue.
///
/// <para>REWRITTEN 2026-08-31. Every input used to be a bare <c>new DateTime(y, m, d)</c> -- a
/// midnight value with no Kind, which is not a thing the production code ever receives. The real
/// inputs are UTC INSTANTS (<c>Appointment.CreationTime</c>), and the deadline is a PACIFIC calendar
/// date, so the old tests could pass while the shipped behaviour was a day out for any request
/// submitted after 5pm Pacific. Every instant below is therefore a real UTC instant, written with
/// its Pacific equivalent in the comment. June is PDT, so Pacific is UTC-7.</para>
/// </summary>
public class DecisionSlaPolicyUnitTests
{
    /// <summary>2026-06-11 12:00 Pacific. Midday on purpose: the UTC and Pacific dates agree, so
    /// each test below reads as its plain intent, and the boundary cases are called out explicitly
    /// rather than being smuggled into every assertion.</summary>
    private static readonly DateTime NowUtc = new(2026, 6, 11, 19, 0, 0, DateTimeKind.Utc);

    /// <summary>2026-06-08 12:00 Pacific -- three days before "today".</summary>
    private static readonly DateTime RequestedThreeDaysAgo = new(2026, 6, 8, 19, 0, 0, DateTimeKind.Utc);

    /// <summary>2026-06-07 12:00 Pacific -- four days before "today".</summary>
    private static readonly DateTime RequestedFourDaysAgo = new(2026, 6, 7, 19, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void DecisionDueDate_IsTheRequestPacificDatePlusTheWindow()
    {
        // Requested 2026-06-08 Pacific, window 3 -> due 2026-06-11.
        DecisionSlaPolicy.DecisionDueDate(RequestedThreeDaysAgo, 3)
            .ShouldBe(new DateTime(2026, 6, 11));
    }

    [Fact]
    public void DecisionDueDate_IgnoresThePacificTimeOfDay()
    {
        // Both instants fall on 2026-06-08 in Pacific -- one at midnight, one at 23:59 -- so both
        // must yield the same due date. This is the assertion the old test was reaching for, but it
        // expressed the time-of-day in UTC, where 23:59 is already the NEXT Pacific day.
        var pacificMidnight = new DateTime(2026, 6, 8, 7, 0, 0, DateTimeKind.Utc);
        var pacificLastMinute = new DateTime(2026, 6, 9, 6, 59, 0, DateTimeKind.Utc);

        DecisionSlaPolicy.DecisionDueDate(pacificMidnight, 3).ShouldBe(new DateTime(2026, 6, 11));
        DecisionSlaPolicy.DecisionDueDate(pacificLastMinute, 3).ShouldBe(new DateTime(2026, 6, 11));
    }

    [Fact]
    public void IsDecisionOverdue_OnTheDueDate_IsFalse()
    {
        // Due today -> still "due today", not overdue.
        DecisionSlaPolicy.IsDecisionOverdue(RequestedThreeDaysAgo, NowUtc, 3).ShouldBeFalse();
    }

    [Fact]
    public void IsDecisionOverdue_TheDayAfterTheDueDate_IsTrue()
    {
        // Requested four days ago -> due 2026-06-10, and today is the 11th.
        DecisionSlaPolicy.IsDecisionOverdue(RequestedFourDaysAgo, NowUtc, 3).ShouldBeTrue();
    }

    [Fact]
    public void IsDecisionOverdue_RequestedToday_IsFalse()
    {
        DecisionSlaPolicy.IsDecisionOverdue(NowUtc, NowUtc, 3).ShouldBeFalse();
    }

    [Fact]
    public void IsDecisionOverdue_ForAnEveningPacificRequest_UsesThePacificDate()
    {
        // THE REGRESSION TEST. 2026-06-08 02:30 UTC is 2026-06-07 19:30 Pacific -- a request
        // submitted on the Sunday evening. Its Pacific date is the 7th, so the deadline is the 10th
        // and it IS overdue on the 11th.
        //
        // The old code took .Date off the raw UTC instant, read the request as the 8th, set the
        // deadline to the 11th, and reported NOT overdue. The consequence was not cosmetic: staff
        // were not told about a request that was genuinely past its decision deadline, on the one
        // surface whose job is to tell them.
        var sundayEveningPacific = new DateTime(2026, 6, 8, 2, 30, 0, DateTimeKind.Utc);

        DecisionSlaPolicy.DecisionDueDate(sundayEveningPacific, 3).ShouldBe(new DateTime(2026, 6, 10));
        DecisionSlaPolicy.IsDecisionOverdue(sundayEveningPacific, NowUtc, 3).ShouldBeTrue();
    }

    [Fact]
    public void OverdueCreationCutoff_IsPacificMidnightExpressedInUtc()
    {
        // Window 3, Pacific today the 11th -> boundary is the 8th. The returned value must be the
        // UTC INSTANT at which Pacific midnight on the 8th occurred (07:00 UTC in PDT), because it
        // is compared against CreationTime inside SQL where the stored values are UTC. Returning a
        // bare 2026-06-08 would skew the comparison by seven hours.
        DecisionSlaPolicy.OverdueCreationCutoff(NowUtc, 3)
            .ShouldBe(new DateTime(2026, 6, 8, 7, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void OverdueCreationCutoff_AgreesWithIsDecisionOverdue()
    {
        // The dashboard runs `CreationTime < cutoff` in SQL; the digest runs IsDecisionOverdue per
        // row. They must select the same rows or the tile count and the email disagree. A row one
        // second before the cutoff is overdue; one exactly at it is not.
        var cutoff = DecisionSlaPolicy.OverdueCreationCutoff(NowUtc, 3);

        DecisionSlaPolicy.IsDecisionOverdue(cutoff.AddSeconds(-1), NowUtc, 3).ShouldBeTrue();
        DecisionSlaPolicy.IsDecisionOverdue(cutoff, NowUtc, 3).ShouldBeFalse();
    }
}
