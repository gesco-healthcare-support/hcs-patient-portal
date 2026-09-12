using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// Unit tests for <see cref="EvaluationKindPolicy"/>. This value is published to the Case Tracker,
/// which labels a patient's case folder from it, so a wrong answer is an operational problem in
/// another system -- worth covering every flow rather than one happy path.
/// </summary>
public class EvaluationKindPolicyUnitTests
{
    [Fact]
    public void RevalFlow_IsAReEvaluation()
    {
        EvaluationKindPolicy.FromLifecycleFlow(AppointmentLifecycleFlow.Reval)
            .ShouldBe(EvaluationKind.ReEvaluation);
    }

    [Fact]
    public void StandardCreate_IsAFirstEvaluation()
    {
        // A plain CreateAsync passes no lifecycle flow.
        EvaluationKindPolicy.FromLifecycleFlow(null).ShouldBe(EvaluationKind.Evaluation);
    }

    [Fact]
    public void ReSubmitFlow_IsAFirstEvaluation_NotAReEvaluation()
    {
        // A re-submit is the SAME evaluation re-entered after a send-back (it even reuses the source
        // confirmation number), so labelling it RE_EVAL would create a spurious follow-up folder.
        EvaluationKindPolicy.FromLifecycleFlow(AppointmentLifecycleFlow.ReSubmit)
            .ShouldBe(EvaluationKind.Evaluation);
    }

    [Fact]
    public void EvaluationKindValues_StartAtOne_SoDefaultIntIsNotValid()
    {
        // The migration backfills existing rows with 1; a 0-based enum would have made EF's generated
        // default(int) look like a legitimate value.
        ((int)EvaluationKind.Evaluation).ShouldBe(1);
        ((int)EvaluationKind.ReEvaluation).ShouldBe(2);
    }

    [Theory]
    [InlineData(EvaluationKind.Evaluation, "EVAL")]
    [InlineData(EvaluationKind.ReEvaluation, "RE_EVAL")]
    public void WireValues_MatchTheAgreedContract(EvaluationKind kind, string expected)
    {
        Integration.CaseTracker.EvaluationKindWire.ToWire(kind).ShouldBe(expected);
    }
}
