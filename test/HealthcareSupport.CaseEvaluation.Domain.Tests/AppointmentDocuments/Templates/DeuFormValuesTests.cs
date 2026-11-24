using System;
using System.Linq;
using HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails;
using HealthcareSupport.CaseEvaluation.Appointments;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments.Templates;

/// <summary>
/// <see cref="DeuFormValues"/>: the values pre-filled on the DWC Employee's Disability Questionnaire (2026-11-24).
/// "" means the template renders a fillable field, so emptiness is pinned as carefully as the values. Synthetic data.
/// </summary>
public class DeuFormValuesTests
{
    private static readonly Guid AppointmentId = Guid.NewGuid();

    private static AppointmentInjuryDetail Injury(
        string claim, DateTime from, DateTime? created = null, Guid? id = null, DateTime? to = null, bool cumulative = false) =>
        new(id ?? Guid.NewGuid(), AppointmentId, from, claim, cumulative, "TEST back", toDateOfInjury: to, wcabAdj: "ADJ-1")
        {
            CreationTime = created ?? new DateTime(2031, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };

    private static AppointmentPatientSnapshot Patient(
        string? first = " TEST-Ann ", string? middle = " marie ", string? last = "TEST-Lee", string? ssn = "000-11-2222",
        string? street = "1 TEST Way", string? unit = "unit 7", string? city = "TEST City", string? zip = "00000") =>
        new(first, middle, last, "ann@test.local", new DateTime(1980, 2, 3), ssn, "555-0100", null, null,
            street, unit, city, null, zip, null, null);

    private static DateTime D(int y, int m, int d) => new(y, m, d, 0, 0, 0, DateTimeKind.Unspecified);

    [Fact]
    public void Every_value_is_trimmed_and_upper_cased_and_the_doctor_is_printed_as_the_office_wrote_it()
    {
        var values = DeuFormValues.Compose(
            Patient(), " TEST-California ", new[] { Injury("clm-1", D(2029, 1, 2)) }, " TEST Employer ",
            " Dr. TEST Doctor ", "12/18/2031", "8:30 AM");

        values.FirstName.ShouldBe("TEST-ANN");
        values.MiddleInitial.ShouldBe("M");
        values.LastName.ShouldBe("TEST-LEE");
        values.Ssn.ShouldBe("000112222", "the form asks for numbers only");
        values.Street1.ShouldBe("1 TEST WAY");
        values.Street2.ShouldBe("UNIT 7");
        values.City.ShouldBe("TEST CITY");
        values.State.ShouldBe("TEST-CALIFORNIA");
        values.Zip.ShouldBe("00000");
        values.DateOfBirth.ShouldBe("02/03/1980");
        values.DateOfInjury.ShouldBe("01/02/2029");
        values.Employer.ShouldBe("TEST EMPLOYER");
        values.Claim1.ShouldBe("CLM-1");
        values.Claim2.ShouldBeEmpty();
        values.Claim5.ShouldBeEmpty();
        values.DoctorName.ShouldBe("Dr. TEST Doctor");
        values.ExamScheduled.ShouldBe("12/18/2031 at 8:30 AM");
    }

    [Fact]
    public void Nothing_known_leaves_every_value_empty_so_every_field_becomes_fillable()
    {
        var values = DeuFormValues.Compose(null, null, Array.Empty<AppointmentInjuryDetail>(), null, null, null, null);

        typeof(DeuFormValues).GetProperties().Where(p => p.PropertyType == typeof(string))
            .Select(p => (string)p.GetValue(values)!).ShouldAllBe(v => v == string.Empty);
        typeof(DeuFormValues).GetProperties().Count(p => p.PropertyType == typeof(string)).ShouldBe(19);
    }

    [Fact]
    public void A_value_of_only_spaces_is_unknown()
    {
        var values = DeuFormValues.Compose(
            Patient(first: "  ", middle: "  ", ssn: " - ", unit: "   "), "  ", Array.Empty<AppointmentInjuryDetail>(),
            "   ", "  ", "  ", "  ");

        values.FirstName.ShouldBeEmpty();
        values.MiddleInitial.ShouldBeEmpty();
        values.Ssn.ShouldBeEmpty("a stored SSN with no digits is no SSN");
        values.Street2.ShouldBeEmpty();
        values.State.ShouldBeEmpty();
        values.Employer.ShouldBeEmpty();
        values.DoctorName.ShouldBeEmpty();
        values.ExamScheduled.ShouldBeEmpty();
    }

    [Fact]
    public void Injuries_are_ordered_by_date_then_creation_then_id_whatever_order_they_load_in()
    {
        var sameDay = D(2029, 5, 5);
        var lowId = new Guid("00000000-0000-0000-0000-000000000001");
        var highId = new Guid("00000000-0000-0000-0000-000000000002");
        var early = new DateTime(2031, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var late = early.AddMinutes(1);
        var injuries = new[]
        {
            Injury("C-LATEST", D(2030, 1, 1)),
            Injury("B-SAME-DAY-HIGH-ID", sameDay, early, highId),
            Injury("B-SAME-DAY-LATER", sameDay, late),
            Injury("A-EARLIEST", D(2028, 1, 1)),
            Injury("B-SAME-DAY-LOW-ID", sameDay, early, lowId),
        };

        var ordered = DeuFormValues.OrderInjuries(injuries).Select(i => i.ClaimNumber).ToArray();
        var reversed = DeuFormValues.OrderInjuries(injuries.Reverse()).Select(i => i.ClaimNumber).ToArray();

        ordered.ShouldBe(new[] { "A-EARLIEST", "B-SAME-DAY-LOW-ID", "B-SAME-DAY-HIGH-ID", "B-SAME-DAY-LATER", "C-LATEST" });
        reversed.ShouldBe(ordered);
    }

    [Fact]
    public void Claims_are_distinct_case_insensitively_and_the_fifth_line_carries_every_one_after_the_fourth()
    {
        var lines = DeuFormValues.ClaimLinesOf(new[] { "c-1", " C-1 ", "c-2", "", null, "c-3", "c-4", "c-5", "c-6", "C-6" });

        lines.ShouldBe(new[] { "C-1", "C-2", "C-3", "C-4", "C-5, C-6" });
    }

    [Fact]
    public void Fewer_claims_leave_the_remaining_lines_empty()
    {
        DeuFormValues.ClaimLinesOf(new[] { "c-1", "c-2" }).ShouldBe(new[] { "C-1", "C-2", "", "", "" });
        DeuFormValues.ClaimLinesOf(Array.Empty<string>()).ShouldAllBe(l => l == string.Empty);
        DeuFormValues.ClaimLinesOf(Array.Empty<string>()).Length.ShouldBe(DeuFormValues.ClaimLines);
    }

    [Fact]
    public void A_cumulative_injury_is_a_range_with_an_en_dash_and_several_injuries_are_joined_in_order()
    {
        var line = DeuFormValues.DateOfInjuryLine(new[]
        {
            Injury("C-1", D(2024, 1, 2), cumulative: true, to: D(2025, 6, 30)),
            Injury("C-2", D(2025, 3, 4)),
        });

        line.ShouldBe("01/02/2024 \u2013 06/30/2025; 03/04/2025");
    }

    [Fact]
    public void An_end_date_without_the_cumulative_flag_or_a_cumulative_injury_without_one_prints_the_start_date()
    {
        DeuFormValues.DateOfInjuryLine(new[] { Injury("C-1", D(2024, 1, 2), to: D(2025, 6, 30)) }).ShouldBe("01/02/2024");
        DeuFormValues.DateOfInjuryLine(new[] { Injury("C-1", D(2024, 1, 2), cumulative: true) }).ShouldBe("01/02/2024");
    }

    [Fact]
    public void The_compose_path_orders_claims_and_dates_together()
    {
        var values = DeuFormValues.Compose(Patient(), "CA", new[]
        {
            Injury("LATER", D(2030, 1, 1)),
            Injury("EARLIER", D(2029, 1, 1), cumulative: true, to: D(2029, 6, 1)),
        }, null, null, null, null);

        values.Claim1.ShouldBe("EARLIER");
        values.Claim2.ShouldBe("LATER");
        values.DateOfInjury.ShouldBe("01/01/2029 \u2013 06/01/2029; 01/01/2030");
    }

    [Theory]
    [InlineData("12/18/2031", "8:30 AM", "12/18/2031 at 8:30 AM")]
    [InlineData("12/18/2031", "", "12/18/2031")]
    [InlineData("", "8:30 AM", "")]
    [InlineData(null, null, "")]
    public void The_exam_line_needs_a_date_and_adds_the_time_when_there_is_one(string? date, string? time, string expected)
    {
        DeuFormValues.ExamScheduledLine(date, time).ShouldBe(expected);
    }
}
