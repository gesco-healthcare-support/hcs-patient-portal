using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments.Jobs;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Enums;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus;
using Volo.Abp.Uow;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments.Handlers;

/// <summary>
/// W2-11: subscribes to <see cref="AppointmentStatusChangedEto"/> and
/// enqueues the packet-generation job whenever an appointment transitions
/// to Approved. Decoupled from the AppService approve path so packet
/// wiring stays out of the booking domain (matches the W1-1 SlotCascade +
/// W1-2 StatusChangeEmail patterns).
/// </summary>
public class PacketGenerationOnApprovedHandler :
    ILocalEventHandler<AppointmentStatusChangedEto>,
    ITransientDependency
{
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly ILogger<PacketGenerationOnApprovedHandler> _logger;

    public PacketGenerationOnApprovedHandler(
        IBackgroundJobManager backgroundJobManager,
        IUnitOfWorkManager unitOfWorkManager,
        ILogger<PacketGenerationOnApprovedHandler> logger)
    {
        _backgroundJobManager = backgroundJobManager;
        _unitOfWorkManager = unitOfWorkManager;
        _logger = logger;
    }

    [UnitOfWork]
    public virtual Task HandleEventAsync(AppointmentStatusChangedEto eventData)
    {
        if (eventData.ToStatus != AppointmentStatusType.Approved)
        {
            return Task.CompletedTask;
        }

        var args = new GenerateAppointmentPacketArgs
        {
            AppointmentId = eventData.AppointmentId,
            TenantId = eventData.TenantId,
        };

        // BUG-036 sub-bug 2: ABP's Hangfire-backed IBackgroundJobManager
        // enqueues immediately (NOT UoW-deferred). Calling EnqueueAsync
        // directly inside [UnitOfWork] lets the Hangfire worker dequeue
        // and start before the approve UoW commits, so the job's
        // GetAsync(appointmentId) can race the parent transaction. Wrap
        // the enqueue in CurrentUnitOfWork.OnCompleted (same pattern as
        // GenerateAppointmentPacketJob.GenerateKindAsync:202-210 for
        // PacketGeneratedEto) so the job is only enqueued AFTER the
        // approve commit succeeds.
        var currentUow = _unitOfWorkManager.Current;
        if (currentUow != null)
        {
            currentUow.OnCompleted(async () =>
            {
                await _backgroundJobManager.EnqueueAsync(args);
                _logger.LogInformation(
                    "PacketGenerationOnApprovedHandler: enqueued packet job for appointment {AppointmentId} (tenant {TenantId}) on UoW commit.",
                    eventData.AppointmentId, eventData.TenantId);
            });
        }
        else
        {
            return EnqueueImmediatelyAsync(args, eventData);
        }

        return Task.CompletedTask;
    }

    private async Task EnqueueImmediatelyAsync(
        GenerateAppointmentPacketArgs args,
        AppointmentStatusChangedEto eventData)
    {
        await _backgroundJobManager.EnqueueAsync(args);
        _logger.LogInformation(
            "PacketGenerationOnApprovedHandler: enqueued packet job for appointment {AppointmentId} (tenant {TenantId}) (no ambient UoW).",
            eventData.AppointmentId, eventData.TenantId);
    }
}
