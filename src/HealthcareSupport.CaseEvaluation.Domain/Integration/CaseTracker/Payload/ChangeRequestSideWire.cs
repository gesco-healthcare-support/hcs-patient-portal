using System;
using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Maps <see cref="ChangeRequestSide"/> to its wire value.
///
/// <para>An explicit mapping rather than <c>ToString()</c>, for the same reason
/// <see cref="EvaluationKindWire"/> exists: the C# names read naturally in code while the contract
/// values are screaming-snake, and serializing the enum name directly would silently change the wire
/// format the moment someone renamed a member.</para>
///
/// <para>THROWS on an unknown value, unlike the inbound <see cref="AttendanceOutcomeWire"/> which
/// try-parses. The difference is direction of travel: a value we cannot map on the way OUT is a
/// programming error and should be loud, whereas untrusted input on the way IN must become a 400
/// rather than a 500.</para>
///
/// <para>What the sides MEAN, for the receiver: Side A is the patient and their applicant attorney;
/// Side B is the defense attorney and the claim examiner. Consent is solicited from the side that
/// did NOT request the change.</para>
/// </summary>
public static class ChangeRequestSideWire
{
    public const string SideA = "SIDE_A";
    public const string SideB = "SIDE_B";

    public static string ToWire(ChangeRequestSide side) => side switch
    {
        ChangeRequestSide.SideA => SideA,
        ChangeRequestSide.SideB => SideB,
        _ => throw new ArgumentOutOfRangeException(nameof(side), side, "No wire value for this side."),
    };

    /// <summary>Null-tolerant companion: a request with no recorded side sends null, not a guess.</summary>
    public static string? ToWireOrNull(ChangeRequestSide? side) => side is { } value ? ToWire(value) : null;
}
