using HealthcareSupport.CaseEvaluation.Patients;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Reports;

/// <summary>
/// G-08-01: the Appointment Request Report and its PDFs show only the birth
/// YEAR, never the full date of birth (Adrian's HIPAA call 2026-06-06).
/// </summary>
public class DobVisibilityTests
{
    [Fact]
    public void ToYearOnly_returns_four_digit_birth_year()
    {
        DobVisibility.ToYearOnly(new DateTime(1985, 3, 12)).ShouldBe("1985");
    }

    [Fact]
    public void ToYearOnly_does_not_leak_month_or_day()
    {
        // Same year, different month/day -> identical output: the exact date never leaks.
        DobVisibility.ToYearOnly(new DateTime(1985, 12, 31))
            .ShouldBe(DobVisibility.ToYearOnly(new DateTime(1985, 1, 1)));
    }

    [Fact]
    public void ToYearOnly_returns_null_when_no_dob()
    {
        DobVisibility.ToYearOnly(null).ShouldBeNull();
    }
}
