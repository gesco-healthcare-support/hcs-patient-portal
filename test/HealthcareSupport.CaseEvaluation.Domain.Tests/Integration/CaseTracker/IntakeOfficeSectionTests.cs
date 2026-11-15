using System;
using HealthcareSupport.CaseEvaluation.Branding;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// <see cref="IntakeOfficeSection.From"/>: blank means null on the wire, and the records-release
/// address always reaches the Case Tracker with bare <c>"\n"</c> row separators. All values synthetic.
/// </summary>
public class IntakeOfficeSectionTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_letterhead_value_is_null_on_the_wire(string blank)
    {
        var letterhead = new OfficeLetterhead
        {
            PracticeName = blank,
            RecordsReleaseAddress = blank,
            Phone = blank,
            Fax = blank,
        };

        var section = IntakeOfficeSection.From(letterhead);

        section.PracticeName.ShouldBeNull();
        section.RecordsReleaseAddress.ShouldBeNull();
        section.Phone.ShouldBeNull();
        section.Fax.ShouldBeNull();
    }

    [Theory]
    [InlineData("TEST ROW 1\nTEST ROW 2\nTEST ROW 3")]
    [InlineData("TEST ROW 1\r\nTEST ROW 2\r\nTEST ROW 3")]
    [InlineData("TEST ROW 1\rTEST ROW 2\r\nTEST ROW 3")]
    public void The_records_release_address_keeps_its_rows_with_bare_newlines(string stored)
    {
        var section = IntakeOfficeSection.From(new OfficeLetterhead { RecordsReleaseAddress = stored });

        section.RecordsReleaseAddress.ShouldBe("TEST ROW 1\nTEST ROW 2\nTEST ROW 3");
    }

    [Fact]
    public void Values_are_sent_as_the_packet_prints_them_without_reformatting()
    {
        var section = IntakeOfficeSection.From(new OfficeLetterhead
        {
            PracticeName = "TEST Sample Spine Center",
            Phone = "(555)010-0100",
            Fax = "555.010.0101",
        });

        section.PracticeName.ShouldBe("TEST Sample Spine Center");
        section.Phone.ShouldBe("(555)010-0100");
        section.Fax.ShouldBe("555.010.0101");
    }

    [Fact]
    public void A_missing_letterhead_is_a_programming_error()
    {
        Should.Throw<ArgumentNullException>(() => IntakeOfficeSection.From(null!));
    }
}
