using System;
using System.Collections.Generic;
using System.Linq;
using HealthcareSupport.CaseEvaluation.CustomFields;
using HealthcareSupport.CaseEvaluation.Enums;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// Pure unit tests for <see cref="AppointmentsAppService.BuildCustomFieldDisplay"/>, the merge that
/// turns "the active fields for this appointment type" plus "the values saved against this
/// appointment" into what the booking form renders.
///
/// <para>No rig: the method is <c>internal static</c> and reachable through InternalsVisibleTo. That
/// is worth saying because the neighbouring custom-field code is NOT -- <c>PersistCustomFieldValues</c>
/// and <c>ReplaceCustomFieldValues</c> are private instance methods behind the create and update
/// paths. This is the part of that seam that can be tested cheaply and it carries the only real
/// decisions in it.</para>
/// </summary>
public class CustomFieldDisplayUnitTests
{
    private static CustomField Field(string label, int displayOrder, Guid? id = null) =>
        new(
            id: id ?? Guid.NewGuid(),
            tenantId: null,
            fieldLabel: label,
            displayOrder: displayOrder,
            fieldType: CustomFieldType.Alphanumeric,
            appointmentTypeId: Guid.NewGuid());

    private static CustomFieldValue Value(Guid fieldId, string value) =>
        new(
            id: Guid.NewGuid(),
            tenantId: null,
            customFieldId: fieldId,
            appointmentId: Guid.NewGuid(),
            value: value);

    [Fact]
    public void BuildCustomFieldDisplay_OrdersByDisplayOrder_NotByInputOrder()
    {
        // The form renders in the order this returns, so the sort IS the behaviour. Input is
        // deliberately supplied backwards: a method that simply passed the collection through
        // would return it in the wrong order and this would catch it.
        var third = Field("Third", displayOrder: 30);
        var first = Field("First", displayOrder: 10);
        var second = Field("Second", displayOrder: 20);

        var result = AppointmentsAppService.BuildCustomFieldDisplay(
            new[] { third, first, second },
            Array.Empty<CustomFieldValue>());

        result.Select(r => r.FieldLabel).ShouldBe(new[] { "First", "Second", "Third" });
    }

    [Fact]
    public void BuildCustomFieldDisplay_AttachesASavedValueToItsOwnField_AndLeavesOthersNull()
    {
        // Both halves matter. Attaching the value proves the join works; the untouched field
        // coming back with a null Value proves the join is keyed rather than blanket -- a merge
        // that assigned the same value to every field would satisfy the first half alone.
        var answered = Field("Answered", displayOrder: 10);
        var unanswered = Field("Unanswered", displayOrder: 20);

        var result = AppointmentsAppService.BuildCustomFieldDisplay(
            new[] { answered, unanswered },
            new[] { Value(answered.Id, "the answer") });

        result.Single(r => r.CustomFieldId == answered.Id).Value.ShouldBe("the answer");
        result.Single(r => r.CustomFieldId == unanswered.Id).Value.ShouldBeNull();
    }

    [Fact]
    public void BuildCustomFieldDisplay_WhenOneFieldHasDuplicateSavedValues_TakesTheFirst()
    {
        // Duplicates are possible: PersistCustomFieldValuesAsync inserts without a uniqueness
        // constraint on (appointment, field), and ReplaceCustomFieldValuesAsync deletes-then-inserts
        // within one unit of work. The merge resolves it by taking First() rather than throwing,
        // so a duplicated row degrades the form instead of breaking it.
        var field = Field("Duplicated", displayOrder: 10);

        var result = AppointmentsAppService.BuildCustomFieldDisplay(
            new[] { field },
            new[] { Value(field.Id, "winner"), Value(field.Id, "loser") });

        result.Count.ShouldBe(1, "A duplicated saved value must not duplicate the rendered field.");
        result[0].Value.ShouldBe("winner");
    }

    [Fact]
    public void BuildCustomFieldDisplay_WithNoActiveFields_ReturnsEmptyEvenWhenValuesExist()
    {
        // Deactivating a field must remove it from the form even though its answers are still
        // stored. The saved value below is deliberately present and deliberately orphaned: an
        // implementation driven from the VALUES rather than the FIELDS would surface it.
        var result = AppointmentsAppService.BuildCustomFieldDisplay(
            Array.Empty<CustomField>(),
            new[] { Value(Guid.NewGuid(), "orphaned answer") });

        result.ShouldBeEmpty(
            "Only ACTIVE fields render. A value whose field is gone must not reappear on the form.");
    }
}
