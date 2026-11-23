using System;
using HealthcareSupport.CaseEvaluation.AppointmentEmployerDetails;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// <see cref="IntakeEmployerSection.From"/>: optional values are null on the wire when blank, the two required
/// values are sent as stored (never invented), and nothing is reformatted or upper-cased. All values synthetic.
/// </summary>
public class IntakeEmployerSectionTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_optional_value_is_null_on_the_wire(string? blank)
    {
        var row = Row("TEST Acme Logistics", "TEST Warehouse Lead");
        row.PhoneNumber = blank;
        row.Street = blank;
        row.City = blank;
        row.ZipCode = blank;

        var section = IntakeEmployerSection.From(row, stateName: blank);

        section.PhoneNumber.ShouldBeNull();
        section.Street.ShouldBeNull();
        section.City.ShouldBeNull();
        section.State.ShouldBeNull();
        section.ZipCode.ShouldBeNull();
    }

    [Fact]
    public void Values_are_sent_raw_not_trimmed_or_upper_cased()
    {
        var row = Row("TEST Acme Logistics, Inc.", "TEST warehouse lead");
        row.PhoneNumber = "(555) 010-0142 ext 7";
        row.Street = " 100 Sample Way ";
        row.City = "Encino";
        row.ZipCode = "91316-0001";

        var section = IntakeEmployerSection.From(row, "California");

        section.Name.ShouldBe("TEST Acme Logistics, Inc.");
        section.Occupation.ShouldBe("TEST warehouse lead");
        section.PhoneNumber.ShouldBe("(555) 010-0142 ext 7");
        section.Street.ShouldBe(" 100 Sample Way ");
        section.City.ShouldBe("Encino");
        section.State.ShouldBe("California");
        section.ZipCode.ShouldBe("91316-0001");
    }

    /// <summary>
    /// Name and occupation are required on the row, but a send-back correction can store them blank. They are sent
    /// as stored: the Case Tracker logs and prints a blank, and the portal never invents a value.
    /// </summary>
    [Fact]
    public void A_blank_name_or_occupation_is_sent_as_stored()
    {
        var section = IntakeEmployerSection.From(Row(string.Empty, string.Empty), stateName: null);

        section.Name.ShouldBe(string.Empty);
        section.Occupation.ShouldBe(string.Empty);
    }

    private static AppointmentEmployerDetail Row(string name, string occupation) =>
        new(Guid.NewGuid(), Guid.NewGuid(), stateId: null, name, occupation);
}
