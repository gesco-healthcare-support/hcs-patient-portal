using System;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Timing;

/// <summary>
/// Tests for <see cref="PacificTime"/>. Pure functions, so no container: every case pins an exact
/// UTC instant and asserts an exact result.
///
/// <para>The DST cases are the point of the class, not decoration. Pacific time repeats
/// 01:00-02:00 every November and skips 02:00-03:00 every March, so any code that reasons about
/// "the local time" without an instant to anchor it has a right answer and a wrong answer and no
/// way to choose. Anchoring on the UTC instant makes the choice for us; these tests prove it does.
/// 2026 transitions: DST starts 2026-03-08 10:00Z, ends 2026-11-01 09:00Z.</para>
/// </summary>
public class PacificTimeTests
{
    [Fact]
    public void Zone_ResolvesToPacificAndNotUtc()
    {
        // The resolver has no UTC fallback on purpose. If this ever fails it means the build or
        // deploy platform lost its timezone database, which would otherwise show up as every
        // surface quietly rendering UTC while claiming Pacific.
        PacificTime.Zone.ShouldNotBe(TimeZoneInfo.Utc);
        PacificTime.Zone.BaseUtcOffset.ShouldBe(TimeSpan.FromHours(-8));
    }

    [Fact]
    public void FromUtc_ConvertsSummerInstantAtPdtOffset()
    {
        var result = PacificTime.FromUtc(new DateTime(2026, 8, 27, 22, 0, 0, DateTimeKind.Utc));

        result.ShouldBe(new DateTime(2026, 8, 27, 15, 0, 0));
        PacificTime.Abbreviation(new DateTime(2026, 8, 27, 22, 0, 0, DateTimeKind.Utc))
            .ShouldBe("PDT");
    }

    [Fact]
    public void FromUtc_ConvertsWinterInstantAtPstOffset()
    {
        var result = PacificTime.FromUtc(new DateTime(2026, 12, 15, 22, 0, 0, DateTimeKind.Utc));

        result.ShouldBe(new DateTime(2026, 12, 15, 14, 0, 0));
        PacificTime.Abbreviation(new DateTime(2026, 12, 15, 22, 0, 0, DateTimeKind.Utc))
            .ShouldBe("PST");
    }

    [Fact]
    public void FromUtc_DistinguishesTheTwoPassesThroughTheRepeatedNovemberHour()
    {
        // DST ends 2026-11-01 at 09:00Z (02:00 PDT falls back to 01:00 PST), so 01:30 local
        // happens TWICE: once at 08:30Z on PDT and again at 09:30Z on PST. Both must render
        // 01:30, and the offset in force must differ -- that is what "correct for the instant
        // rather than guessed" means.
        var firstPass = new DateTime(2026, 11, 1, 8, 30, 0, DateTimeKind.Utc);
        var secondPass = new DateTime(2026, 11, 1, 9, 30, 0, DateTimeKind.Utc);

        PacificTime.FromUtc(firstPass).ShouldBe(new DateTime(2026, 11, 1, 1, 30, 0));
        PacificTime.FromUtc(secondPass).ShouldBe(new DateTime(2026, 11, 1, 1, 30, 0));

        PacificTime.Abbreviation(firstPass).ShouldBe("PDT");
        PacificTime.Abbreviation(secondPass).ShouldBe("PST");
    }

    [Fact]
    public void FromUtc_HandlesTheSkippedMarchHour()
    {
        // DST starts 2026-03-08 at 10:00Z: 02:00 PST jumps to 03:00 PDT, so no local time in
        // 02:00-02:59 exists that day. One minute of UTC either side of the transition must land
        // on either side of the gap, never inside it.
        PacificTime.FromUtc(new DateTime(2026, 3, 8, 9, 59, 0, DateTimeKind.Utc))
            .ShouldBe(new DateTime(2026, 3, 8, 1, 59, 0));

        PacificTime.FromUtc(new DateTime(2026, 3, 8, 10, 0, 0, DateTimeKind.Utc))
            .ShouldBe(new DateTime(2026, 3, 8, 3, 0, 0));
    }

    [Fact]
    public void FromUtc_TreatsUnspecifiedInputAsUtc()
    {
        // This is the shape EF Core hands back for a datetime2 column, and storage is UTC by
        // decision, so it must behave identically to an explicitly-kinded value.
        var asRead = new DateTime(2026, 8, 27, 22, 0, 0, DateTimeKind.Unspecified);
        var asStamped = new DateTime(2026, 8, 27, 22, 0, 0, DateTimeKind.Utc);

        PacificTime.FromUtc(asRead).ShouldBe(PacificTime.FromUtc(asStamped));
    }

    [Fact]
    public void FromUtc_ReturnsUnspecifiedKind()
    {
        // A wall-clock reading is neither UTC nor the reader's local time. Kinding it Local would
        // invite ToUniversalTime() at some later call site and shift it by the READER's offset.
        PacificTime.FromUtc(new DateTime(2026, 8, 27, 22, 0, 0, DateTimeKind.Utc))
            .Kind.ShouldBe(DateTimeKind.Unspecified);
    }

    [Fact]
    public void FromUtc_RejectsLocalKind()
    {
        // Server-local time has no defined meaning here, so a Local input is a call-site bug and
        // must not be silently reinterpreted. The BCL enforces this; the test pins that it stays so.
        Should.Throw<ArgumentException>(
            () => PacificTime.FromUtc(new DateTime(2026, 8, 27, 22, 0, 0, DateTimeKind.Local)));
    }

    [Fact]
    public void FromUtcOrNull_KeepsAbsentAbsent()
    {
        PacificTime.FromUtcOrNull(null).ShouldBeNull();
    }

    [Fact]
    public void TodayFrom_ReturnsThePacificDayNotTheUtcDay()
    {
        // THE REGRESSION TEST for the defect this whole change exists to fix. 23:00 Pacific on the
        // 27th is 06:00 UTC on the 28th, so DateTime.Today on a UTC server answers "the 28th" for
        // the last 7-8 hours of every Pacific day. On a generated packet or a booking horizon that
        // is a whole calendar day wrong.
        var eveningInPacific = new DateTime(2026, 8, 28, 6, 0, 0, DateTimeKind.Utc);

        PacificTime.TodayFrom(eveningInPacific).ShouldBe(new DateTime(2026, 8, 27));
        PacificTime.TodayFrom(eveningInPacific).ShouldNotBe(eveningInPacific.Date);
    }

    [Fact]
    public void TodayFrom_IsMidnightAndUnspecified()
    {
        var result = PacificTime.TodayFrom(new DateTime(2026, 8, 27, 22, 34, 56, DateTimeKind.Utc));

        result.TimeOfDay.ShouldBe(TimeSpan.Zero);
        result.Kind.ShouldBe(DateTimeKind.Unspecified);
    }

    [Fact]
    public void FormatDate_UsesThePacificCalendarDate()
    {
        PacificTime.FormatDate(new DateTime(2026, 8, 28, 6, 0, 0, DateTimeKind.Utc))
            .ShouldBe("08/27/2026");
    }

    [Fact]
    public void FormatDateTime_CarriesTheZoneAbbreviation()
    {
        PacificTime.FormatDateTime(new DateTime(2026, 8, 27, 22, 5, 0, DateTimeKind.Utc))
            .ShouldBe("08/27/2026 3:05 PM PDT");

        PacificTime.FormatDateTime(new DateTime(2026, 12, 15, 22, 5, 0, DateTimeKind.Utc))
            .ShouldBe("12/15/2026 2:05 PM PST");
    }
}
