using HealthcareSupport.CaseEvaluation.Enums;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Maps the inbound <c>outcome</c> wire value to its <see cref="AppointmentStatusType"/>.
///
/// <para>An explicit mapping rather than <c>Enum.Parse</c>, for the same reason
/// <see cref="EvaluationKindWire"/> exists: the C# names read naturally in code while the agreed
/// contract values are <c>NO_SHOW</c> / <c>NOT_SEEN</c>, and parsing the enum name directly would
/// silently change the accepted wire format the moment someone renamed a member. It would also
/// accept every OTHER status name -- including <c>Approved</c> -- turning one endpoint into a
/// general-purpose status setter.</para>
///
/// <para>TRY-parse rather than the throwing switch its outbound siblings use. That difference is the
/// direction of travel: a bad value on the way OUT is a programming error and should blow up, but a
/// bad value on the way IN is untrusted caller input and must become a 400, not a 500.</para>
/// </summary>
public static class AttendanceOutcomeWire
{
    public const string NoShow = "NO_SHOW";
    public const string NotSeen = "NOT_SEEN";

    /// <summary>
    /// True when <paramref name="wire"/> is a recognised outcome. Case-sensitive and untrimmed on
    /// purpose: the contract states two exact values, and quietly accepting <c>no_show</c> or
    /// <c>" NO_SHOW "</c> would let the two systems drift apart without anyone noticing.
    /// </summary>
    public static bool TryParse(string? wire, out AppointmentStatusType outcome)
    {
        switch (wire)
        {
            case NoShow:
                outcome = AppointmentStatusType.NoShow;
                return true;
            case NotSeen:
                outcome = AppointmentStatusType.NotSeen;
                return true;
            default:
                outcome = default;
                return false;
        }
    }
}
