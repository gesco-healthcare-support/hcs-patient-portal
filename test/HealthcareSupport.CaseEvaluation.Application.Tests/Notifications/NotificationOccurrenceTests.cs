using System;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Notifications;

/// <summary>
/// The three ways an email's context tag gets its occurrence part. What matters is the boundary each
/// one draws: two DIFFERENT occurrences must give two different tags (or the second email collapses
/// onto the first and is never sent), and the SAME occurrence must give the same tag (or a replayed
/// unit of work emails twice).
/// </summary>
public class NotificationOccurrenceTests
{
    [Fact]
    public void Two_requests_get_two_tags_under_the_same_kind()
    {
        var first = NotificationOccurrence.PerRequest("PasswordReset/RequestLink/u1");
        var second = NotificationOccurrence.PerRequest("PasswordReset/RequestLink/u1");

        first.ShouldNotBe(second);
        first.ShouldStartWith("PasswordReset/RequestLink/u1/r");
        second.ShouldStartWith("PasswordReset/RequestLink/u1/r");
    }

    [Fact]
    public void The_same_event_time_gives_the_same_tag_and_a_later_one_does_not()
    {
        var at = new DateTime(2026, 10, 23, 19, 30, 0, DateTimeKind.Utc);

        NotificationOccurrence.At("CaseTrackerFeedAlert/SilenceStarted/o1", at)
            .ShouldBe(NotificationOccurrence.At("CaseTrackerFeedAlert/SilenceStarted/o1", at));
        NotificationOccurrence.At("CaseTrackerFeedAlert/SilenceStarted/o1", at.AddTicks(1))
            .ShouldNotBe(NotificationOccurrence.At("CaseTrackerFeedAlert/SilenceStarted/o1", at));
        NotificationOccurrence.At("k", at).ShouldBe($"k/t{at.Ticks}");
    }

    [Fact]
    public void An_event_without_a_time_is_its_own_occurrence()
    {
        // A publisher that forgot OccurredAt must not put every such event back on one shared tag.
        NotificationOccurrence.At("k", default)
            .ShouldNotBe(NotificationOccurrence.At("k", default));
    }

    [Fact]
    public void The_daily_digest_changes_tag_at_Pacific_midnight_not_UTC_midnight()
    {
        // 06:59 UTC on Oct 23 is still Oct 22 in Pacific daylight time; 07:00 UTC is Oct 23.
        var lateOnThe22nd = new DateTime(2026, 10, 23, 6, 59, 0, DateTimeKind.Utc);
        var earlyOnThe23rd = new DateTime(2026, 10, 23, 7, 0, 0, DateTimeKind.Utc);
        var laterOnThe23rd = new DateTime(2026, 10, 23, 16, 0, 0, DateTimeKind.Utc);

        NotificationOccurrence.PerPacificDay("PendingDailyDigest/o1", lateOnThe22nd)
            .ShouldBe("PendingDailyDigest/o1/2026-10-22");
        NotificationOccurrence.PerPacificDay("PendingDailyDigest/o1", earlyOnThe23rd)
            .ShouldBe("PendingDailyDigest/o1/2026-10-23");
        NotificationOccurrence.PerPacificDay("PendingDailyDigest/o1", laterOnThe23rd)
            .ShouldBe(NotificationOccurrence.PerPacificDay("PendingDailyDigest/o1", earlyOnThe23rd),
                "a second run on the same Pacific day is the same digest");
    }

    [Fact]
    public void A_twice_daily_run_has_its_own_tag_for_the_morning_and_the_afternoon()
    {
        // 08:00 and 16:00 Pacific daylight time on Oct 23 = 15:00 and 23:00 UTC.
        var morning = new DateTime(2026, 10, 23, 15, 0, 0, DateTimeKind.Utc);
        var morningRetry = new DateTime(2026, 10, 23, 15, 40, 0, DateTimeKind.Utc);
        var afternoon = new DateTime(2026, 10, 23, 23, 0, 0, DateTimeKind.Utc);
        var nextMorning = new DateTime(2026, 10, 24, 15, 0, 0, DateTimeKind.Utc);

        NotificationOccurrence.PerPacificRun("OfficeDailySummary/o1", morning)
            .ShouldBe("OfficeDailySummary/o1/2026-10-23/AM");
        NotificationOccurrence.PerPacificRun("OfficeDailySummary/o1", afternoon)
            .ShouldBe("OfficeDailySummary/o1/2026-10-23/PM");
        NotificationOccurrence.PerPacificRun("OfficeDailySummary/o1", nextMorning)
            .ShouldBe("OfficeDailySummary/o1/2026-10-24/AM");
        NotificationOccurrence.PerPacificRun("OfficeDailySummary/o1", morningRetry)
            .ShouldBe(NotificationOccurrence.PerPacificRun("OfficeDailySummary/o1", morning),
                "a retry of the same run is the same email");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void A_tag_needs_a_kind(string? kind)
    {
        Should.Throw<ArgumentException>(() => NotificationOccurrence.PerRequest(kind!));
    }

    [Fact]
    public void Every_tag_fits_the_outbox_context_column()
    {
        // The longest kind in use is around 90 characters; the column holds 256.
        var kind = "AccountActivation/IdentityUserCreated/" + Guid.NewGuid() + "/" + new string('x', 40);
        NotificationOccurrence.PerRequest(kind).Length.ShouldBeLessThanOrEqualTo(256);
    }
}
