using System;
using HealthcareSupport.CaseEvaluation.Appointments;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Maps <see cref="EvaluationKind"/> to its wire value.
///
/// <para>An explicit mapping rather than <c>ToString()</c> on purpose: the C# names
/// (<c>Evaluation</c> / <c>ReEvaluation</c>) read naturally in code, while the agreed contract
/// values are <c>EVAL</c> / <c>RE_EVAL</c>. Serializing the enum name directly would silently
/// change the wire format the moment someone renamed the enum member.</para>
/// </summary>
public static class EvaluationKindWire
{
    public const string Evaluation = "EVAL";
    public const string ReEvaluation = "RE_EVAL";

    public static string ToWire(EvaluationKind kind) => kind switch
    {
        EvaluationKind.Evaluation => Evaluation,
        EvaluationKind.ReEvaluation => ReEvaluation,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "No wire value for this evaluation kind."),
    };
}
