using HealthcareSupport.CaseEvaluation.Enums;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Reports;

/// <summary>
/// G-08-01: the report query guards, ported from the legacy client-side rules.
/// At least one filter is required; a date range is both-or-neither with
/// From &lt;= To; and the default sort is confirmation number descending.
/// </summary>
public class ReportFilterValidatorTests
{
    [Fact]
    public void HasAnyFilter_is_false_for_an_empty_input()
    {
        ReportFilterValidator.HasAnyFilter(new GetAppointmentReportInput()).ShouldBeFalse();
    }

    [Fact]
    public void HasAnyFilter_is_false_when_filter_text_is_only_whitespace()
    {
        ReportFilterValidator.HasAnyFilter(new GetAppointmentReportInput { FilterText = "   " })
            .ShouldBeFalse();
    }

    [Fact]
    public void HasAnyFilter_is_true_for_filter_text()
    {
        ReportFilterValidator.HasAnyFilter(new GetAppointmentReportInput { FilterText = "Doe" })
            .ShouldBeTrue();
    }

    [Fact]
    public void HasAnyFilter_is_true_for_appointment_type()
    {
        ReportFilterValidator.HasAnyFilter(new GetAppointmentReportInput { AppointmentTypeId = Guid.NewGuid() })
            .ShouldBeTrue();
    }

    [Fact]
    public void HasAnyFilter_is_true_for_location()
    {
        ReportFilterValidator.HasAnyFilter(new GetAppointmentReportInput { LocationId = Guid.NewGuid() })
            .ShouldBeTrue();
    }

    [Fact]
    public void HasAnyFilter_is_true_for_status()
    {
        ReportFilterValidator.HasAnyFilter(new GetAppointmentReportInput { AppointmentStatus = AppointmentStatusType.Pending })
            .ShouldBeTrue();
    }

    [Fact]
    public void HasAnyFilter_is_true_for_a_date_bound()
    {
        ReportFilterValidator.HasAnyFilter(new GetAppointmentReportInput { AppointmentDateMin = new DateTime(2026, 1, 1) })
            .ShouldBeTrue();
    }

    [Fact]
    public void IsDateRangeValid_true_when_no_dates()
    {
        ReportFilterValidator.IsDateRangeValid(null, null).ShouldBeTrue();
    }

    [Fact]
    public void IsDateRangeValid_false_when_only_from()
    {
        ReportFilterValidator.IsDateRangeValid(new DateTime(2026, 1, 1), null).ShouldBeFalse();
    }

    [Fact]
    public void IsDateRangeValid_false_when_only_to()
    {
        ReportFilterValidator.IsDateRangeValid(null, new DateTime(2026, 1, 1)).ShouldBeFalse();
    }

    [Fact]
    public void IsDateRangeValid_true_when_from_before_to()
    {
        ReportFilterValidator.IsDateRangeValid(new DateTime(2026, 1, 1), new DateTime(2026, 1, 31)).ShouldBeTrue();
    }

    [Fact]
    public void IsDateRangeValid_true_when_from_equals_to()
    {
        var d = new DateTime(2026, 1, 15);
        ReportFilterValidator.IsDateRangeValid(d, d).ShouldBeTrue();
    }

    [Fact]
    public void IsDateRangeValid_false_when_from_after_to()
    {
        ReportFilterValidator.IsDateRangeValid(new DateTime(2026, 2, 1), new DateTime(2026, 1, 1)).ShouldBeFalse();
    }

    [Fact]
    public void ResolveSorting_returns_default_when_blank()
    {
        ReportFilterValidator.ResolveSorting(null).ShouldBe(ReportFilterValidator.DefaultSorting);
        ReportFilterValidator.ResolveSorting("   ").ShouldBe(ReportFilterValidator.DefaultSorting);
    }

    [Fact]
    public void ResolveSorting_default_is_confirmation_number_descending()
    {
        ReportFilterValidator.DefaultSorting.ShouldBe("Appointment.RequestConfirmationNumber desc");
    }

    [Fact]
    public void ResolveSorting_passes_through_an_explicit_sort()
    {
        ReportFilterValidator.ResolveSorting("Appointment.AppointmentDate asc")
            .ShouldBe("Appointment.AppointmentDate asc");
    }
}
