using System;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.ExternalAccount;

/// <summary>
/// Pins the progressive lockout ladder (item D, 2026-08-22): 1 -> 5 -> 15 minutes, then the
/// configured maximum.
///
/// <para>All the decision logic for item D lives in this one pure function precisely so it can be
/// pinned here. Lockout itself is enforced by <c>SignInManager</c> on the AuthServer, which has no
/// test project, so anything left inside the replacement <c>IdentityUserManager</c> would only ever
/// be verifiable by hand.</para>
/// </summary>
public class LockoutBackoffTests
{
    private static readonly TimeSpan OneHour = TimeSpan.FromHours(1);

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 5)]
    [InlineData(3, 15)]
    public void DurationForCycle_WalksTheLadderForTheFirstThreeCycles(int cycle, int expectedMinutes)
    {
        LockoutBackoff.DurationForCycle(cycle, OneHour)
            .ShouldBe(TimeSpan.FromMinutes(expectedMinutes));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(50)]
    public void DurationForCycle_UsesTheConfiguredMaximumFromTheFourthCycleOn(int cycle)
    {
        // The ceiling never rises above today's flat behaviour, so this change can only shorten a
        // lockout, never lengthen one.
        LockoutBackoff.DurationForCycle(cycle, OneHour).ShouldBe(OneHour);
    }

    [Fact]
    public void DurationForCycle_TreatsACycleBelowOneAsTheFirstLockout()
    {
        // The counter is read from a JSON extension property; an absent or malformed value must
        // degrade to the shortest lockout rather than break the sign-in path.
        LockoutBackoff.DurationForCycle(0, OneHour).ShouldBe(TimeSpan.FromMinutes(1));
        LockoutBackoff.DurationForCycle(-7, OneHour).ShouldBe(TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void DurationForCycle_CapsARungAtAShorterConfiguredMaximum()
    {
        // The setting is the policy. If an administrator configures 2 minutes, cycle 3's 15-minute
        // rung must not override it.
        var twoMinutes = TimeSpan.FromMinutes(2);

        LockoutBackoff.DurationForCycle(1, twoMinutes).ShouldBe(TimeSpan.FromMinutes(1));
        LockoutBackoff.DurationForCycle(2, twoMinutes).ShouldBe(twoMinutes);
        LockoutBackoff.DurationForCycle(3, twoMinutes).ShouldBe(twoMinutes);
        LockoutBackoff.DurationForCycle(9, twoMinutes).ShouldBe(twoMinutes);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(99)]
    public void DurationForCycle_NeverExceedsTheConfiguredMaximum(int cycle)
    {
        // The invariant that matters most: whatever the ladder says, the configured policy is the
        // ceiling. Checked across several maxima including degenerate ones.
        foreach (var max in new[]
                 {
                     TimeSpan.Zero,
                     TimeSpan.FromSeconds(30),
                     TimeSpan.FromMinutes(10),
                     OneHour,
                     TimeSpan.FromDays(1),
                 })
        {
            LockoutBackoff.DurationForCycle(cycle, max)
                .ShouldBeLessThanOrEqualTo(max, $"cycle {cycle} with max {max}");
        }
    }
}
