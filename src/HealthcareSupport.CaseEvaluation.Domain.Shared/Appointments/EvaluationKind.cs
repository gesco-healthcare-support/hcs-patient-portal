namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// Whether an appointment is a first evaluation or a re-evaluation of an earlier one.
///
/// <para>Persisted explicitly rather than inferred from
/// <c>Appointment.OriginalAppointmentId</c>: that column is still documented as a
/// reschedule-chain link (it predates the 2026-07-01 in-place-reschedule redesign, which
/// no longer writes it), so deriving from it would silently mislabel if that ever changes.
/// The Case Tracker uses this value to label a case folder, so a wrong label is an
/// operational problem in another system -- worth an explicit column.</para>
///
/// <para>Stored as an int (codebase convention -- see <c>AppointmentStatusType</c>,
/// <c>DocumentStatus</c>). The integration wire format is the string <c>"EVAL"</c> /
/// <c>"RE_EVAL"</c>, produced by an explicit mapping in the payload builder rather than by
/// serializing this name.</para>
/// </summary>
public enum EvaluationKind
{
    /// <summary>First evaluation. Wire value <c>"EVAL"</c>.</summary>
    Evaluation = 1,

    /// <summary>Re-evaluation of a prior appointment. Wire value <c>"RE_EVAL"</c>.</summary>
    ReEvaluation = 2,
}
