using System;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Branding;

/// <summary>
/// 2026-10-09 (walkthrough Q5) -- the packet letterhead an office prints. Two halves: what
/// <see cref="OfficeBranding.SetLetterhead"/> stores, and how <see cref="OfficeLetterhead.Compose"/>
/// fills the gaps so a practice nobody configured still prints its OWN doctor, never another
/// office's. All values synthetic.
/// </summary>
public class OfficeLetterheadTests
{
    private static OfficeBranding NewBranding() => new(Guid.NewGuid(), Guid.NewGuid());

    // -- what is stored --------------------------------------------------------------------

    [Fact]
    public void SetLetterhead_trims_and_stores_blank_as_null()
    {
        var branding = NewBranding();

        branding.SetLetterhead(new OfficeLetterheadValues
        {
            LetterheadName = "  TEST Heading  ",
            PhysicianName = "   ",
            Phone = "",
            MissedAppointmentFee = 12.5m,
        });

        branding.LetterheadName.ShouldBe("TEST Heading");
        branding.PhysicianName.ShouldBeNull();
        branding.Phone.ShouldBeNull();
        branding.Fax.ShouldBeNull();
        branding.MissedAppointmentFee.ShouldBe(12.5m);
    }

    [Fact]
    public void SetLetterhead_replaces_every_field_so_an_omitted_one_is_cleared()
    {
        var branding = NewBranding();
        branding.SetLetterhead(new OfficeLetterheadValues { Fax = "555-0101", MissedAppointmentFee = 1m });

        branding.SetLetterhead(new OfficeLetterheadValues { Phone = "555-0100" });

        branding.Phone.ShouldBe("555-0100");
        branding.Fax.ShouldBeNull();
        branding.MissedAppointmentFee.ShouldBeNull();
    }

    [Fact]
    public void SetLetterhead_normalizes_the_release_address_to_one_trimmed_row_per_line()
    {
        var branding = NewBranding();

        branding.SetLetterhead(new OfficeLetterheadValues
        {
            RecordsReleaseAddress = "  1 TEST WAY,\r\n\r\n  SUITE 9 \r\nTEST CITY, CA 90001\n",
        });

        branding.RecordsReleaseAddress.ShouldBe("1 TEST WAY,\nSUITE 9\nTEST CITY, CA 90001");
    }

    [Fact]
    public void SetLetterhead_release_address_of_only_blank_lines_is_cleared()
    {
        var branding = NewBranding();
        branding.SetLetterhead(new OfficeLetterheadValues { RecordsReleaseAddress = " \r\n \n" });
        branding.RecordsReleaseAddress.ShouldBeNull();
    }

    [Fact]
    public void SetLetterhead_refuses_a_negative_fee()
    {
        Should.Throw<ArgumentException>(() =>
            NewBranding().SetLetterhead(new OfficeLetterheadValues { MissedAppointmentFee = -0.01m }));
    }

    [Fact]
    public void SetLetterhead_refuses_an_over_long_field()
    {
        var tooLong = new string('x', OfficeLetterheadConsts.PhoneMaxLength + 1);
        Should.Throw<ArgumentException>(() =>
            NewBranding().SetLetterhead(new OfficeLetterheadValues { Fax = tooLong }));
    }

    [Fact]
    public void GetLetterhead_returns_what_was_stored()
    {
        var branding = NewBranding();
        var values = new OfficeLetterheadValues
        {
            LetterheadName = "A", LetterheadTagline = "B", PhysicianName = "C", PracticeName = "D",
            MailingStreet = "E", MailingCity = "F", MailingState = "G", MailingZip = "H",
            Phone = "I", Fax = "J", RecordsDeliveryAddress = "K", RecordsReleaseAddress = "L",
            MissedAppointmentFee = 2m,
        };

        branding.SetLetterhead(values);

        branding.GetLetterhead().ShouldBe(values);
    }

    // -- how the gaps are filled -----------------------------------------------------------

    [Fact]
    public void A_new_office_prints_its_own_doctor_with_no_setup()
    {
        var letterhead = OfficeLetterhead.Compose(null, null, "TEST-Ada", "TEST-Example");

        letterhead.PhysicianName.ShouldBe("Dr. TEST-Ada TEST-Example");
        letterhead.LetterheadName.ShouldBe("Dr. TEST-Ada TEST-Example");
        letterhead.PracticeName.ShouldBe("Dr. TEST-Ada TEST-Example");
        letterhead.LetterheadTagline.ShouldBeEmpty();
        letterhead.MailingAddress.ShouldBeEmpty();
        letterhead.PhoneFax.ShouldBeEmpty();
        letterhead.MissedAppointmentFee.ShouldBeEmpty();
    }

    [Fact]
    public void The_practice_name_defaults_to_the_display_name_before_the_physician()
    {
        var letterhead = OfficeLetterhead.Compose(null, "TEST Spine Group", "TEST-Ada", "TEST-Example");

        letterhead.PracticeName.ShouldBe("TEST Spine Group");
        letterhead.PhysicianName.ShouldBe("Dr. TEST-Ada TEST-Example");
    }

    [Fact]
    public void Stored_values_win_over_every_default()
    {
        var values = new OfficeLetterheadValues
        {
            LetterheadName = "TEST-Ada Example, M.D., FTEST",
            PhysicianName = "TEST-Ada Example, M.D.",
            PracticeName = "TEST Institute",
        };

        var letterhead = OfficeLetterhead.Compose(values, "TEST Display", "TEST-Ada", "TEST-Example");

        letterhead.LetterheadName.ShouldBe("TEST-Ada Example, M.D., FTEST");
        letterhead.PhysicianName.ShouldBe("TEST-Ada Example, M.D.");
        letterhead.PracticeName.ShouldBe("TEST Institute");
    }

    [Fact]
    public void The_heading_falls_back_to_the_stored_physician_not_the_derived_one()
    {
        var values = new OfficeLetterheadValues { PhysicianName = "TEST-Ada Example, D.O." };

        OfficeLetterhead.Compose(values, null, "TEST-Ada", "TEST-Example")
            .LetterheadName.ShouldBe("TEST-Ada Example, D.O.");
    }

    [Fact]
    public void With_no_doctor_the_physician_falls_back_to_the_display_name_never_a_bare_title()
    {
        OfficeLetterhead.Compose(null, "TEST Office", null, "  ").PhysicianName.ShouldBe("TEST Office");
        OfficeLetterhead.Compose(null, null, null, null).PhysicianName.ShouldBeEmpty();
        OfficeLetterhead.DefaultPhysicianName(" ", null).ShouldBeNull();
    }

    [Theory]
    [InlineData("P.O. Box 1", "TEST City", "CA", "90001", "P.O. Box 1, TEST City, CA 90001")]
    [InlineData(null, "TEST City", "CA", "90001", "TEST City, CA 90001")]
    [InlineData("P.O. Box 1", null, "CA", "90001", "P.O. Box 1, CA 90001")]
    [InlineData("P.O. Box 1", "TEST City", null, null, "P.O. Box 1, TEST City")]
    [InlineData(null, null, null, null, "")]
    public void The_mailing_address_line_skips_whatever_is_blank(
        string? street, string? city, string? state, string? zip, string expected)
    {
        var values = new OfficeLetterheadValues
        {
            MailingStreet = street, MailingCity = city, MailingState = state, MailingZip = zip,
        };

        OfficeLetterhead.Compose(values, null, "A", "B").MailingAddress.ShouldBe(expected);
    }

    [Theory]
    [InlineData("555-0100", "555-0101", "Phone: 555-0100 • Fax: 555-0101")]
    [InlineData("555-0100", null, "Phone: 555-0100")]
    [InlineData(null, "555-0101", "Fax: 555-0101")]
    [InlineData(null, null, "")]
    public void The_phone_fax_line_skips_whatever_is_blank(string? phone, string? fax, string expected)
    {
        OfficeLetterhead.Compose(new OfficeLetterheadValues { Phone = phone, Fax = fax }, null, "A", "B")
            .PhoneFax.ShouldBe(expected);
    }

    [Theory]
    [InlineData("503.75", "503.75")]
    [InlineData("250", "250.00")]
    [InlineData("1200.5", "1,200.50")]
    public void The_fee_prints_as_dollars_and_cents(string stored, string expected)
    {
        var values = new OfficeLetterheadValues
        {
            MissedAppointmentFee = decimal.Parse(stored, System.Globalization.CultureInfo.InvariantCulture),
        };

        OfficeLetterhead.Compose(values, null, "A", "B").MissedAppointmentFee.ShouldBe(expected);
    }
}
