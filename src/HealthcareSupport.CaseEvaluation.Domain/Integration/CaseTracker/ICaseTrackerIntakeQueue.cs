using System;
using System.Threading;
using System.Threading.Tasks;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// The intake enqueue seam. Extracted in Part 2 so the change trigger is unit-testable without the
/// payload builder's five repositories behind it -- that handler's logic is entirely about WHICH
/// appointments to re-push, which is what its tests should exercise.
/// </summary>
public interface ICaseTrackerIntakeQueue
{
    /// <summary>
    /// Renders the intake payload and enqueues it once for this version of the appointment. A replay
    /// for unchanged state collapses onto the existing row.
    /// </summary>
    Task<IntegrationOutboxItem> EnqueueIntakeAsync(
        Guid appointmentId,
        Guid? tenantId,
        CancellationToken cancellationToken = default);
}
