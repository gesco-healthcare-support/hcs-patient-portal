using System;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments.Templates;

/// <summary>
/// #710 -- the packet generated-on date must be stamped in PACIFIC.
///
/// <para>A packet generated after about 5pm Pacific was stamped with TOMORROW's
/// date, on a document served as a legal record of an appointment. The fix has
/// been correct since 2026-08-27 and nothing would have caught its removal: the
/// rule was asserted on <c>PacificTime</c> and on nothing at the boundary that
/// uses it.</para>
///
/// <para>EVERY EXPECTED VALUE HERE IS A LITERAL. The trap this issue names is
/// asserting <c>DateNow == Format(PacificTime.TodayFrom(now))</c>, which
/// compares the code to itself and passes forever, including with the Pacific
/// conversion deleted. Literals are what make the mutation fail.</para>
/// </summary>
public class PacketDateStampTests
{
    // The instant named in the issue's acceptance criterion: 02:30 UTC on
    // 9 September is 19:30 Pacific on the 8th. UTC says the 9th, Pacific the 8th.
    private static readonly DateTime EveningPacific =
        new(2026, 9, 9, 2, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void GeneratedOn_StampsThePacificDate_NotTheUtcOne()
    {
        // THE WHOLE TEST. A mid-day instant proves nothing, because UTC and
        // Pacific agree on the calendar date for most of the day.
        PacketDateStamp.GeneratedOn(EveningPacific).ShouldBe("09/08/2026");
    }

    [Fact]
    public void GeneratedOn_AgreesWithUtcOutsideTheWindow()
    {
        // 18:00 UTC on 8 September is 11:00 Pacific the same day. This is the
        // ~16 hours a day during which the original defect was invisible, which
        // is why it survived to production.
        PacketDateStamp.GeneratedOn(new DateTime(2026, 9, 8, 18, 0, 0, DateTimeKind.Utc))
            .ShouldBe("09/08/2026");
    }

    [Fact]
    public void GeneratedOn_CrossesTheMonthBoundaryInPacificTerms()
    {
        // 01:00 UTC on 1 October is 18:00 Pacific on 30 September. The UTC
        // reading changes the MONTH as well as the day, so a month-end packet
        // was stamped into the wrong month entirely.
        PacketDateStamp.GeneratedOn(new DateTime(2026, 10, 1, 1, 0, 0, DateTimeKind.Utc))
            .ShouldBe("09/30/2026");
    }

    [Fact]
    public void GeneratedOn_CrossesTheYearBoundaryInPacificTerms()
    {
        // 02:00 UTC on 1 January is 18:00 Pacific on 31 December.
        PacketDateStamp.GeneratedOn(new DateTime(2027, 1, 1, 2, 0, 0, DateTimeKind.Utc))
            .ShouldBe("12/31/2026");
    }

    [Fact]
    public void GeneratedOn_HandlesStandardTimeAsWellAsDaylight()
    {
        // January is PST (UTC-8), not PDT (UTC-7). 03:00 UTC on 16 January is
        // 19:00 Pacific on the 15th. A fixed -7 offset would answer the 16th
        // here, so this separates a real timezone conversion from a subtraction.
        PacketDateStamp.GeneratedOn(new DateTime(2026, 1, 16, 3, 0, 0, DateTimeKind.Utc))
            .ShouldBe("01/15/2026");
    }

    [Fact]
    public void Format_RendersTheOldPattern()
    {
        PacketDateStamp.Format(new DateTime(2026, 9, 8)).ShouldBe("09/08/2026");
        PacketDateStamp.Format(new DateTime(2026, 12, 31)).ShouldBe("12/31/2026");
    }

    [Fact]
    public void Format_RendersNullAsEmpty_NotTheWordNull()
    {
        // An absent date renders as an empty token. The alternative on a legal
        // document is the literal text "null" or a crash mid-render.
        PacketDateStamp.Format(null).ShouldBe(string.Empty);
    }
}
