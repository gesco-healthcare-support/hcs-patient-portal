using System;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Integration.CaseTracker.Jobs;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Uow;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Shared enqueue path for an intake push: build the payload, write the outbox row, and kick the
/// drain. Extracted so the approval handler and the manual "Push to Case Tracker" action cannot
/// drift apart -- the manual action exists precisely to re-drive what the automatic one does.
/// </summary>
public class CaseTrackerIntakeQueue : ICaseTrackerIntakeQueue, ITransientDependency
{
    private readonly IIntakePayloadBuilder _payloadBuilder;
    private readonly IntegrationOutboxManager _outboxManager;
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly ILogger<CaseTrackerIntakeQueue> _logger;

    public CaseTrackerIntakeQueue(
        IIntakePayloadBuilder payloadBuilder,
        IntegrationOutboxManager outboxManager,
        IBackgroundJobManager backgroundJobManager,
        IUnitOfWorkManager unitOfWorkManager,
        ILogger<CaseTrackerIntakeQueue> logger)
    {
        _payloadBuilder = payloadBuilder;
        _outboxManager = outboxManager;
        _backgroundJobManager = backgroundJobManager;
        _unitOfWorkManager = unitOfWorkManager;
        _logger = logger;
    }

    /// <summary>
    /// Renders the intake payload and enqueues it exactly once for this version of the appointment.
    /// Returns the ledger row (existing or new).
    ///
    /// <para>The row is written through the repository so it joins whatever unit of work is ambient
    /// -- during an approval that is the approval's own transaction, making the enqueue atomic with
    /// the state change that caused it.</para>
    /// </summary>
    public virtual async Task<IntegrationOutboxItem> EnqueueIntakeAsync(
        Guid appointmentId,
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        var envelope = await _payloadBuilder.BuildAsync(appointmentId, cancellationToken);
        var payloadJson = IntakePayloadSerializer.Serialize(envelope);

        // Version the key by the appointment's own UpdatedAt: a replayed event for the SAME state
        // collapses onto the existing row, while a genuinely newer version enqueues a fresh push.
        var idempotencyKey = IntegrationOutboxManager.BuildIdempotencyKey(
            IntegrationMessageType.Intake,
            appointmentId,
            envelope.Data.UpdatedAt);

        var row = await _outboxManager.EnqueueAsync(
            tenantId,
            IntegrationMessageType.Intake,
            CaseTrackerEndpoints.Intake,
            appointmentId,
            payloadJson,
            idempotencyKey);

        ScheduleDrain(tenantId, appointmentId);

        return row;
    }

    /// <summary>
    /// Enqueues the drain job AFTER the current unit of work commits.
    ///
    /// <para>ABP's Hangfire-backed <see cref="IBackgroundJobManager"/> enqueues immediately, NOT on
    /// UoW commit. Calling it inline would let a worker dequeue and query for the outbox row before
    /// the surrounding transaction committed, so the drain would find nothing and the push would wait
    /// for the 15-minute sweep. Deferring to <c>OnCompleted</c> removes that race -- the same fix
    /// <c>PacketGenerationOnApprovedHandler</c> applies.</para>
    /// </summary>
    private void ScheduleDrain(Guid? tenantId, Guid appointmentId)
    {
        var args = new IntegrationOutboxDrainArgs { TenantId = tenantId };
        var currentUow = _unitOfWorkManager.Current;

        if (currentUow == null)
        {
            // No ambient UoW: the row is already committed, so enqueue directly.
            _ = _backgroundJobManager.EnqueueAsync(args);
            return;
        }

        currentUow.OnCompleted(async () =>
        {
            try
            {
                await _backgroundJobManager.EnqueueAsync(args);
            }
            catch (ObjectDisposedException ex)
            {
                // The callback runs after commit; in the in-memory test stack the lifetime scope the
                // job store captured can already be disposed. Losing one enqueue is recoverable --
                // the row is committed and the reconciliation sweep re-drives it -- whereas
                // propagating would fail an otherwise successful approval.
                _logger.LogWarning(
                    ex,
                    "CaseTrackerIntakeQueue: drain enqueue skipped for appointment {AppointmentId} -- DI scope disposed before OnCompleted fired.",
                    appointmentId);
            }
        });
    }
}
