using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments.Events;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus;
using Volo.Abp.Uow;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker.Handlers;

/// <summary>
/// Queues the Case Tracker intake push when an appointment is approved.
///
/// <para>Lives in the Application layer, not Domain, because <see cref="AppointmentApprovedEto"/> is
/// declared in Application.Contracts and Domain cannot reference upward. Mirrors
/// <c>PackageDocumentQueueHandler</c>, which subscribes to a sibling Application.Contracts event for
/// the same reason. The reusable work itself lives in Domain
/// (<see cref="CaseTrackerIntakeQueue"/>).</para>
///
/// <para>Fires on approval and does NOT wait for packets. Packet rendering is an asynchronous
/// Hangfire job finishing seconds-to-minutes later, so the first push normally carries zero
/// documents; the receiver upserts on the appointment id and accepts an empty document set, and the
/// packets follow through the document-update feed. Waiting instead would need a stateful
/// all-three-generated aggregator plus a timeout path for a permanently failed packet, and would buy
/// the receiver nothing it needs.</para>
/// </summary>
public class CaseTrackerIntakeOnApprovedHandler :
    ILocalEventHandler<AppointmentApprovedEto>,
    ITransientDependency
{
    private readonly CaseTrackerIntakeQueue _intakeQueue;
    private readonly ILogger<CaseTrackerIntakeOnApprovedHandler> _logger;

    public CaseTrackerIntakeOnApprovedHandler(
        CaseTrackerIntakeQueue intakeQueue,
        ILogger<CaseTrackerIntakeOnApprovedHandler> logger)
    {
        _intakeQueue = intakeQueue;
        _logger = logger;
    }

    [UnitOfWork]
    public virtual async Task HandleEventAsync(AppointmentApprovedEto eventData)
    {
        if (eventData == null)
        {
            return;
        }

        try
        {
            var row = await _intakeQueue.EnqueueIntakeAsync(eventData.AppointmentId, eventData.TenantId);

            _logger.LogInformation(
                "CaseTrackerIntakeOnApprovedHandler: appointment {AppointmentId} queued for Case Tracker intake (row {RowId}).",
                eventData.AppointmentId, row.Id);
        }
        catch (Exception ex)
        {
            // An integration failure must never fail the approval itself -- staff approving an
            // appointment is the primary business action and the push is downstream of it. The
            // appointment stays approved and the manual "Push to Case Tracker" action re-drives it.
            _logger.LogError(
                ex,
                "CaseTrackerIntakeOnApprovedHandler: failed to queue intake for appointment {AppointmentId}; the approval stands and the push must be re-driven manually.",
                eventData.AppointmentId);
        }
    }
}
