using System;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EmailStops;

/// <summary>
/// 2026-11-05 -- the pure half of the email stops: how an address is matched, which scope wins, and how one
/// email is re-addressed when some of its addresses are stopped. All addresses are synthetic.
/// </summary>
public class EmailStopLookupTests
{
    private static readonly Guid CaseA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid CaseB = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");

    private static EmailStopLookup Lookup(string[]? allCases = null, (Guid, string)[]? caseStops = null) =>
        new(
            EmailStopAddress.NormalizeAll(allCases ?? Array.Empty<string>()),
            Array.ConvertAll(caseStops ?? Array.Empty<(Guid, string)>(), c => (c.Item1, EmailStopAddress.Normalize(c.Item2)!)));

    [Fact]
    public void A_case_stop_holds_only_on_its_own_case_even_in_a_batch_that_mixes_cases()
    {
        var lookup = Lookup(caseStops: new[] { (CaseA, "ann@firm.test") });

        lookup.ScopeOf("ann@firm.test", CaseA).ShouldBe(EmailStopScope.Case);
        lookup.IsStopped("ann@firm.test", CaseB).ShouldBeFalse();
        lookup.IsStopped("ann@firm.test", null).ShouldBeFalse("an email about no case honours all-cases stops only");
    }

    [Fact]
    public void An_all_cases_stop_holds_everywhere_and_wins_over_a_case_stop()
    {
        var lookup = Lookup(new[] { "ann@firm.test" }, new[] { (CaseA, "ann@firm.test") });

        lookup.ScopeOf("ann@firm.test", CaseA).ShouldBe(EmailStopScope.AllCases);
        lookup.ScopeOf("ann@firm.test", CaseB).ShouldBe(EmailStopScope.AllCases);
        lookup.ScopeOf("ann@firm.test", null).ShouldBe(EmailStopScope.AllCases);
    }

    [Fact]
    public void Addresses_match_whatever_their_case_and_surrounding_spaces()
    {
        var lookup = Lookup(new[] { " Ann@Firm.TEST " });

        lookup.IsStopped("ann@firm.test", null).ShouldBeTrue();
        lookup.IsStopped("  ANN@firm.test", CaseA).ShouldBeTrue();
        lookup.IsStopped("bob@firm.test", CaseA).ShouldBeFalse();
        lookup.IsStopped(null, CaseA).ShouldBeFalse();
        lookup.IsStopped("   ", CaseA).ShouldBeFalse();
    }

    [Fact]
    public void The_empty_lookup_stops_nothing()
    {
        EmailStopLookup.Empty.IsEmpty.ShouldBeTrue();
        EmailStopLookup.Empty.IsStopped("ann@firm.test", CaseA).ShouldBeFalse();
        Lookup(new[] { "ann@firm.test" }).IsEmpty.ShouldBeFalse();
    }

    [Fact]
    public void A_stopped_to_hands_the_email_to_the_first_remaining_cc()
    {
        var lookup = Lookup(new[] { "ann@firm.test" });

        var result = EmailStopAddressing.Apply("ann@firm.test", new[] { "bob@firm.test", "cy@firm.test" }, lookup, CaseA);

        result.To.ShouldBe("bob@firm.test");
        result.Cc.ShouldBe(new[] { "cy@firm.test" });
        result.Dropped.ShouldBe(new[] { "ann@firm.test" });
        result.IsDropped.ShouldBeFalse();
        result.Changed.ShouldBeTrue();
    }

    [Fact]
    public void A_stopped_cc_is_left_off_and_the_to_stays()
    {
        var lookup = Lookup(caseStops: new[] { (CaseA, "bob@firm.test") });

        var result = EmailStopAddressing.Apply("ann@firm.test", new[] { "bob@firm.test", "cy@firm.test", " " }, lookup, CaseA);

        result.To.ShouldBe("ann@firm.test");
        result.Cc.ShouldBe(new[] { "cy@firm.test" });
        result.Dropped.ShouldBe(new[] { "bob@firm.test" });
    }

    [Fact]
    public void When_everyone_is_stopped_nothing_is_sent()
    {
        var lookup = Lookup(new[] { "ann@firm.test", "bob@firm.test" });

        var result = EmailStopAddressing.Apply("ann@firm.test", new[] { "bob@firm.test" }, lookup, null);

        result.IsDropped.ShouldBeTrue();
        result.To.ShouldBeNull();
        result.Dropped.ShouldBe(new[] { "ann@firm.test", "bob@firm.test" });
    }

    [Fact]
    public void Nothing_stopped_leaves_the_email_as_it_was()
    {
        var result = EmailStopAddressing.Apply("ann@firm.test", new[] { "bob@firm.test" }, EmailStopLookup.Empty, CaseA);

        result.To.ShouldBe("ann@firm.test");
        result.Cc.ShouldBe(new[] { "bob@firm.test" });
        result.Changed.ShouldBeFalse();
    }

    [Theory]
    [InlineData("ann@firm.test", "a***@firm.test")]
    [InlineData("  Ann@Firm.test ", "A***@Firm.test")]
    [InlineData("@firm.test", "***")]
    [InlineData("not-an-address", "***")]
    [InlineData(null, "(blank)")]
    public void A_log_line_never_carries_the_full_address(string? input, string expected)
    {
        EmailStopAddress.Mask(input).ShouldBe(expected);
    }

    [Fact]
    public void The_stop_rows_store_the_normalized_address()
    {
        var global = new GlobalEmailStop(Guid.NewGuid(), " Ann@Firm.TEST ", null, null, DateTime.UtcNow, null, null, "  ");
        global.Email.ShouldBe("Ann@Firm.TEST");
        global.NormalizedEmail.ShouldBe("ANN@FIRM.TEST");
        global.Note.ShouldBeNull();

        var perCase = new AppointmentEmailStop(Guid.NewGuid(), null, CaseA, "ann@firm.test", null, new string('x', 300), DateTime.UtcNow, "TEST note");
        perCase.NormalizedEmail.ShouldBe("ANN@FIRM.TEST");
        perCase.StoppedByName!.Length.ShouldBe(EmailStopConsts.NameMaxLength);
        perCase.Note.ShouldBe("TEST note");
    }
}
