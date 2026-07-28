using HealthcareSupport.CaseEvaluation.Enums;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Whether an appointment's intake has been published, and therefore whether a follow-up message
/// about it can land. One definition shared by every document and change trigger, so they cannot
/// disagree about which appointments the Case Tracker knows.
/// </summary>
public static class CaseTrackerPublishPolicy
{
    /// <summary>
    /// True once approval has pushed the intake. Expressed as a DENY list rather than an allow list
    /// on purpose: the three excluded states are all reachable only from <c>Pending</c>, so they are
    /// a closed set, whereas post-approval states are open-ended. If a new lifecycle state is added
    /// later and nobody updates this, treating it as published makes the mistake LOUD -- a push that
    /// 404s and dead-letters -- instead of silently letting cases go stale on their side.
    /// </summary>
    public static bool IsPublished(AppointmentStatusType status) => status switch
    {
        // Never approved, so no case exists on the Case Tracker side.
        AppointmentStatusType.Pending => false,
        AppointmentStatusType.Rejected => false,
        AppointmentStatusType.InfoRequested => false,
        _ => true,
    };
}
