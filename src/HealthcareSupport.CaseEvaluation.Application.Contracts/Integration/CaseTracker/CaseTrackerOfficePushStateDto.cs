using System;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// One office's Case Tracker push state, for the host-side admin screen.
/// </summary>
public class CaseTrackerOfficePushStateDto
{
    public Guid OfficeId { get; set; }

    /// <summary>
    /// Resolved from the tenant store, NOT from <c>ICurrentTenant.Name</c> -- entering an office via
    /// <c>ICurrentTenant.Change(id)</c> sets the id and leaves the name null, which previously shipped
    /// a blank office in both a UI column and an alert email.
    /// </summary>
    public string OfficeName { get; set; } = string.Empty;

    public bool PushEnabled { get; set; }

    /// <summary>
    /// Outbox rows currently waiting in this office.
    ///
    /// <para>Shown next to the toggle because enabling an office does not start from empty: while the
    /// push is off the drain claims nothing, so due rows accumulate as Pending and are ALL flushed on
    /// the next drain once enabled. This count is what tells an operator whether flipping the switch
    /// sends one message or several hundred.</para>
    /// </summary>
    public int PendingCount { get; set; }
}
