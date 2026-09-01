using System;
using System.Linq;
using HealthcareSupport.CaseEvaluation.CustomFields;
using HealthcareSupport.CaseEvaluation.Enums;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// PR2 (2026-07-10) -- pure unit tests for
/// <see cref="AppointmentsAppService.BuildCustomFieldDisplay"/>, the read-only
/// projection behind GetAppointmentCustomFieldValuesAsync. Bypasses the ABP
/// integration harness (the test host crashes under the license blocker) via the
/// existing InternalsVisibleTo wiring -- same pattern as CustomFieldsAppServiceUnitTests.
/// </summary>
public class CustomFieldDisplayMappingUnitTests
{
    private static CustomField Field(
        Guid id,
        string label,
        int order,
        CustomFieldType type = CustomFieldType.Alphanumeric)
        => new CustomField(id, tenantId: null, label, order, type, appointmentTypeId: Guid.NewGuid());

    private static CustomFieldValue Value(Guid fieldId, string value)
        => new CustomFieldValue(Guid.NewGuid(), tenantId: null, fieldId, appointmentId: Guid.NewGuid(), value);

    [Fact]
    public void OrdersByDisplayOrder_AndLeftJoinsValues()
    {
        var f1 = Guid.NewGuid();
        var f2 = Guid.NewGuid();
        var f3 = Guid.NewGuid();
        var fields = new[]
        {
            Field(f2, "Second", 2),
            Field(f1, "First", 1),
            Field(f3, "Third", 3),
        };
        var values = new[]
        {
            Value(f1, "answer-1"),
            Value(f3, "answer-3"),
            // f2 intentionally unanswered
        };

        var result = AppointmentsAppService.BuildCustomFieldDisplay(fields, values);

        result.Select(r => r.FieldLabel).ShouldBe(new[] { "First", "Second", "Third" });
        result[0].Value.ShouldBe("answer-1");
        result[1].Value.ShouldBeNull(); // unanswered -> null so the view shows "empty"
        result[2].Value.ShouldBe("answer-3");
    }

    [Fact]
    public void CarriesLabelTypeAndOrder()
    {
        var id = Guid.NewGuid();

        var result = AppointmentsAppService.BuildCustomFieldDisplay(
            new[] { Field(id, "Shoe size", 7, CustomFieldType.Numeric) },
            Array.Empty<CustomFieldValue>());

        var row = result.ShouldHaveSingleItem();
        row.CustomFieldId.ShouldBe(id);
        row.FieldLabel.ShouldBe("Shoe size");
        row.FieldType.ShouldBe(CustomFieldType.Numeric);
        row.DisplayOrder.ShouldBe(7);
        row.Value.ShouldBeNull();
    }

    [Fact]
    public void NoActiveFields_ReturnsEmpty_EvenWithOrphanValues()
    {
        // A value whose field is not in the active set (e.g. deactivated) is dropped.
        var result = AppointmentsAppService.BuildCustomFieldDisplay(
            Array.Empty<CustomField>(),
            new[] { Value(Guid.NewGuid(), "orphan") });

        result.ShouldBeEmpty();
    }
}
