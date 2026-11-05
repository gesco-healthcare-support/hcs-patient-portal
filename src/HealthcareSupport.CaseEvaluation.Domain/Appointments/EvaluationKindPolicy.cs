namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// Decides an appointment's <see cref="EvaluationKind"/> from the booking lifecycle flow.
///
/// <para>Extracted as a pure policy (mirroring <c>RescheduleInPlacePolicy</c> and
/// <c>StatusPillPolicy</c>) so the decision is unit-testable without standing up the whole booking
/// pipeline. The Case Tracker labels a case folder from this value, so it is worth testing
/// exhaustively rather than through one incidental integration path.</para>
/// </summary>
public static class EvaluationKindPolicy
{
    /// <summary>
    /// <see cref="AppointmentLifecycleFlow.Reval"/> and <see cref="AppointmentLifecycleFlow.RevalWithoutSource"/>
    /// (the first evaluation predates this portal) are the follow-up flows. A standard create
    /// (null flow) and a <see cref="AppointmentLifecycleFlow.ReSubmit"/> are both FIRST evaluations:
    /// a re-submit is the same evaluation re-entered after a send-back, not a new one -- which is
    /// also why it reuses the source's confirmation number while a reval mints a fresh one.
    /// </summary>
    public static EvaluationKind FromLifecycleFlow(AppointmentLifecycleFlow? lifecycleFlow)
    {
        return lifecycleFlow is AppointmentLifecycleFlow.Reval or AppointmentLifecycleFlow.RevalWithoutSource
            ? EvaluationKind.ReEvaluation
            : EvaluationKind.Evaluation;
    }
}
