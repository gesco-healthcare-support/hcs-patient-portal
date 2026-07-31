using System;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentDrafts;

/// <summary>
/// #15 (2026-06-22) -- entity-level invariants for the server-persisted booking
/// draft. TDD: pins the ctor + UpdatePayload guards (payload required, label
/// length-bounded) so the self-scoped app service and the purge job can rely on
/// a valid row. Pure unit tests (no DB), mirroring DoctorAvailabilityTests.
/// </summary>
public class AppointmentDraftTests
{
    private const string SamplePayload = "{\"v\":{\"firstName\":\"Jane\"},\"step\":1}";

    private static AppointmentDraft Build(string payloadJson = SamplePayload, string? label = "AME")
    {
        return new AppointmentDraft(
            id: Guid.NewGuid(),
            payloadJson: payloadJson,
            currentStep: 1,
            lastSavedTime: new DateTime(2026, 6, 22, 10, 0, 0, DateTimeKind.Utc),
            label: label);
    }

    [Fact]
    public void Ctor_WhenPayloadJsonIsBlank_Throws()
    {
        Should.Throw<ArgumentException>(() => Build(payloadJson: "   "));
    }

    [Fact]
    public void Ctor_WhenLabelExceedsMaxLength_Throws()
    {
        var tooLong = new string('x', AppointmentDraftConsts.LabelMaxLength + 1);
        var ex = Should.Throw<ArgumentException>(() => Build(label: tooLong));
        ex.ParamName.ShouldBe("label");
    }

    [Fact]
    public void Ctor_SetsFields_AndAllowsNullLabel()
    {
        var draft = Build(label: null);

        draft.PayloadJson.ShouldBe(SamplePayload);
        draft.CurrentStep.ShouldBe(1);
        draft.Label.ShouldBeNull();
        draft.LastSavedTime.ShouldBe(new DateTime(2026, 6, 22, 10, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void UpdatePayload_ReplacesPayloadStepLabelAndTimestamp()
    {
        var draft = Build();
        var newTime = new DateTime(2026, 6, 23, 9, 30, 0, DateTimeKind.Utc);

        draft.UpdatePayload(
            payloadJson: "{\"v\":{\"lastName\":\"Doe\"},\"step\":4}",
            currentStep: 4,
            lastSavedTime: newTime,
            label: "IME");

        draft.PayloadJson.ShouldBe("{\"v\":{\"lastName\":\"Doe\"},\"step\":4}");
        draft.CurrentStep.ShouldBe(4);
        draft.Label.ShouldBe("IME");
        draft.LastSavedTime.ShouldBe(newTime);
    }

    [Fact]
    public void UpdatePayload_WhenPayloadJsonIsBlank_Throws()
    {
        var draft = Build();
        Should.Throw<ArgumentException>(() =>
            draft.UpdatePayload(
                payloadJson: "",
                currentStep: 2,
                lastSavedTime: DateTime.UtcNow));
    }
}
