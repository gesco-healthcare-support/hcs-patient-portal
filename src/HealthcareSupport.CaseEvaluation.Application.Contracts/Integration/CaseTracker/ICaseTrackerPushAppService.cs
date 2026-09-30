using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Staff-facing surface for (re-)pushing an appointment to the Case Tracker.
///
/// <para>Exists as the recovery tool the fail-fast retry policy implies: a push dead-letters after
/// three attempts (~10 minutes) so a human is told early, and this is how that human re-drives it
/// once the cause is fixed. It is also how appointments approved BEFORE the integration was enabled
/// get sent, since the automatic trigger only fires on new approvals.</para>
/// </summary>
public interface ICaseTrackerPushAppService : IApplicationService
{
    /// <summary>
    /// Queues an intake push for the given appointment. Idempotent: re-invoking for an unchanged
    /// appointment collapses onto the existing ledger row rather than sending a duplicate.
    /// </summary>
    Task<CaseTrackerPushQueuedDto> PushAppointmentAsync(Guid appointmentId);
}

/// <summary>Outcome of a manual push request.</summary>
public class CaseTrackerPushQueuedDto
{
    /// <summary>The appointment that was queued.</summary>
    public Guid AppointmentId { get; set; }

    /// <summary>The ledger row id, for correlating with the dead-letter view.</summary>
    public Guid OutboxItemId { get; set; }

    /// <summary>Current ledger status name (<c>Pending</c> immediately after queueing).</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// False when the push is queued but the office has the integration switched off, so nothing will
    /// actually be sent until it is enabled. Lets the UI tell staff why nothing happened.
    /// </summary>
    public bool PushEnabled { get; set; }
}
