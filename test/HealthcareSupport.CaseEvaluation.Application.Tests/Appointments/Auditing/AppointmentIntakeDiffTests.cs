using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Appointments.Auditing;

/// <summary>
/// Group K T4: the intake-changed email diff only reports fields that actually
/// changed, formats date/time readably, and routes values through the PHI policy.
/// </summary>
public class AppointmentIntakeDiffTests
{
    private static readonly DateTime D1 = new(2026, 6, 1, 9, 0, 0);
    private static readonly DateTime D2 = new(2026, 6, 8, 13, 30, 0);

    [Fact]
    public void Reports_only_the_fields_that_changed()
    {
        var rows = AppointmentIntakeDiff.Compute(
            oldAppointmentDate: D1,
            newAppointmentDate: D2,
            oldPanelNumber: "P1",
            newPanelNumber: "P1", // unchanged -> skipped
            oldDueDate: new DateTime(2026, 6, 20),
            newDueDate: new DateTime(2026, 6, 25));

        rows.Select(r => r.PropertyName).ShouldBe(new[] { "AppointmentDate", "DueDate" }, ignoreOrder: true);
        var date = rows.Single(r => r.PropertyName == "AppointmentDate");
        date.ValueRedacted.ShouldBeFalse();
        date.NewValue!.ShouldContain("06/08/2026");
    }

    [Fact]
    public void Empty_when_nothing_changed()
    {
        var rows = AppointmentIntakeDiff.Compute(D1, D1, "P1", "P1", null, null);
        rows.ShouldBeEmpty();
    }

    [Fact]
    public void Date_or_time_change_is_detected()
    {
        AppointmentIntakeDiff.IsDateOrTimeChanged(D1, D2).ShouldBeTrue();
        AppointmentIntakeDiff.IsDateOrTimeChanged(D1, D1).ShouldBeFalse();
    }
}
