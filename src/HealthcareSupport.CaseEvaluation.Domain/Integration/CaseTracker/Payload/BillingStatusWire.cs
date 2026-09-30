using HealthcareSupport.CaseEvaluation.Enums;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Maps an <see cref="AppointmentStatusType"/> to the Case Tracker's explicit billing-intent
/// wire value.
///
/// <para>Phase 2 (2026-07-31). Billing intent used to reach the receiver only IMPLICITLY, encoded
/// in the status string (<c>CancelledNoBill</c> vs <c>CancelledLate</c>), which forced them to
/// string-match our enum spelling to decide whether to bill a case. This surfaces it as its own
/// field so that decision needs no parsing and survives a future rename of the enum member --
/// the same reasoning as <see cref="EvaluationKindWire"/>, and the reason neither type uses
/// <c>ToString()</c>.</para>
///
/// <para>Unlike <see cref="EvaluationKindWire"/> this does NOT throw on an unmapped value: every
/// appointment carries a billing status on the wire, and most statuses simply have no billing
/// intent. Returning <see cref="None"/> keeps the field non-nullable so the receiver never has to
/// tell "absent" apart from "nothing to bill".</para>
/// </summary>
public static class BillingStatusWire
{
    /// <summary>Cancelled or rescheduled without a charge.</summary>
    public const string NoBill = "NO_BILL";

    /// <summary>Cancelled or rescheduled late enough to be billable.</summary>
    public const string Late = "LATE";

    /// <summary>No billing intent -- the appointment is not in a billing-bearing state.</summary>
    public const string None = "NONE";

    /// <summary>
    /// The billing intent implied by <paramref name="status"/>. The status string remains
    /// authoritative for lifecycle; this only answers "bill or not".
    /// </summary>
    public static string ToWire(AppointmentStatusType status) => status switch
    {
        AppointmentStatusType.CancelledNoBill => NoBill,
        AppointmentStatusType.RescheduledNoBill => NoBill,
        AppointmentStatusType.CancelledLate => Late,
        AppointmentStatusType.RescheduledLate => Late,
        _ => None,
    };
}
