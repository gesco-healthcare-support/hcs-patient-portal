using System;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.ExternalAccount;

/// <summary>
/// Pins the LockedOut page's wording (item D, 2026-08-22). The page used to hard-code "1 hour", which
/// the progressive ladder makes wrong for most lockouts -- a first offence is one minute.
///
/// <para>The direction of rounding is the point: stating slightly too long is harmless, stating too
/// short sends someone back to a still-locked account.</para>
/// </summary>
public class LockoutRemainingTextTests
{
    [Theory]
    [InlineData(1, "about 1 minute")]
    [InlineData(59, "about 1 minute")]
    [InlineData(60, "about 1 minute")]
    public void Describe_TreatsAnythingUpToAMinuteAsOneMinute(int seconds, string expected)
    {
        LockoutRemainingText.Describe(TimeSpan.FromSeconds(seconds)).ShouldBe(expected);
    }

    [Theory]
    [InlineData(61, "about 2 minutes")]
    [InlineData(300, "about 5 minutes")]
    [InlineData(301, "about 6 minutes")]
    [InlineData(900, "about 15 minutes")]
    public void Describe_RoundsMinutesUpSoTheAdviceIsNeverEarly(int seconds, string expected)
    {
        LockoutRemainingText.Describe(TimeSpan.FromSeconds(seconds)).ShouldBe(expected);
    }

    [Fact]
    public void Describe_ReadsTheLadderRungsThemselves()
    {
        // The four durations the ladder actually produces.
        LockoutRemainingText.Describe(TimeSpan.FromMinutes(1)).ShouldBe("about 1 minute");
        LockoutRemainingText.Describe(TimeSpan.FromMinutes(5)).ShouldBe("about 5 minutes");
        LockoutRemainingText.Describe(TimeSpan.FromMinutes(15)).ShouldBe("about 15 minutes");
        LockoutRemainingText.Describe(TimeSpan.FromMinutes(60)).ShouldBe("about 1 hour");
    }

    [Theory]
    [InlineData(3601, "about 2 hours")]
    [InlineData(7200, "about 2 hours")]
    [InlineData(7201, "about 3 hours")]
    public void Describe_RoundsHoursUpOncePastAnHour(int seconds, string expected)
    {
        LockoutRemainingText.Describe(TimeSpan.FromSeconds(seconds)).ShouldBe(expected);
    }

    [Fact]
    public void Describe_FallsBackWhenTheRemainderIsUnknownOrAlreadyElapsed()
    {
        // The redirect carries no user id, so "unknown" is a normal case here, not an error. A
        // negative remainder must never render as "in -3 minutes".
        LockoutRemainingText.Describe(null).ShouldBe(LockoutRemainingText.Unknown);
        LockoutRemainingText.Describe(TimeSpan.Zero).ShouldBe(LockoutRemainingText.Unknown);
        LockoutRemainingText.Describe(TimeSpan.FromSeconds(-30)).ShouldBe(LockoutRemainingText.Unknown);
        LockoutRemainingText.Describe(TimeSpan.FromHours(-2)).ShouldBe(LockoutRemainingText.Unknown);
    }

    [Fact]
    public void Describe_NeverStatesATimeShorterThanTheRealOne()
    {
        // The invariant behind the rounding direction, checked across the whole range rather than at
        // hand-picked points.
        for (var seconds = 1; seconds <= 7200; seconds += 7)
        {
            var actual = TimeSpan.FromSeconds(seconds);
            var text = LockoutRemainingText.Describe(actual);

            var statedMinutes = ExtractNumber(text);
            var stated = text.Contains("hour")
                ? TimeSpan.FromHours(statedMinutes)
                : TimeSpan.FromMinutes(statedMinutes);

            stated.ShouldBeGreaterThanOrEqualTo(actual, $"stated '{text}' for {actual}");
        }
    }

    private static int ExtractNumber(string text)
    {
        foreach (var token in text.Split(' '))
        {
            if (int.TryParse(token, out var n))
            {
                return n;
            }
        }

        return 0;
    }
}
