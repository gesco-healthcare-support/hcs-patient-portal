using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Notifications;

/// <summary>
/// Pure unit tests for <see cref="ReminderCadence"/> (no DB / DI). Covers CSV
/// anchor parsing (whitespace, blanks, dupes, non-int, negatives) and the
/// <see cref="ReminderCadence.ShouldFire"/> membership predicate. Synthetic
/// values only.
/// </summary>
public class ReminderCadenceTests
{
    [Fact]
    public void Parses_csv_into_anchor_set()
    {
        var cadence = new ReminderCadence("30, 60 ,75");

        cadence.Anchors.ShouldBe(new[] { 30, 60, 75 }, ignoreOrder: true);
    }

    [Fact]
    public void ShouldFire_is_anchor_membership()
    {
        var cadence = new ReminderCadence("30,60,75");

        cadence.ShouldFire(30).ShouldBeTrue();
        cadence.ShouldFire(60).ShouldBeTrue();
        cadence.ShouldFire(75).ShouldBeTrue();
        cadence.ShouldFire(61).ShouldBeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_or_blank_never_fires(string? csv)
    {
        var cadence = new ReminderCadence(csv);

        cadence.Anchors.ShouldBeEmpty();
        cadence.ShouldFire(0).ShouldBeFalse();
        cadence.ShouldFire(7).ShouldBeFalse();
    }

    [Fact]
    public void Collapses_duplicate_anchors()
    {
        var cadence = new ReminderCadence("7,7,1,1");

        cadence.Anchors.ShouldBe(new[] { 7, 1 }, ignoreOrder: true);
    }

    [Fact]
    public void Ignores_non_integer_tokens()
    {
        var cadence = new ReminderCadence("7,abc,3");

        cadence.Anchors.ShouldBe(new[] { 7, 3 }, ignoreOrder: true);
        cadence.ShouldFire(7).ShouldBeTrue();
    }

    [Fact]
    public void Ignores_negative_tokens()
    {
        var cadence = new ReminderCadence("-1,7");

        cadence.Anchors.ShouldBe(new[] { 7 }, ignoreOrder: true);
        cadence.ShouldFire(-1).ShouldBeFalse();
    }

    [Fact]
    public void Allows_zero_anchor_for_same_day_firing()
    {
        var cadence = new ReminderCadence("0,7");

        cadence.ShouldFire(0).ShouldBeTrue();
        cadence.ShouldFire(7).ShouldBeTrue();
    }

    [Fact]
    public void Skips_empty_csv_segments()
    {
        var cadence = new ReminderCadence("14,,7,");

        cadence.Anchors.ShouldBe(new[] { 14, 7 }, ignoreOrder: true);
    }
}
