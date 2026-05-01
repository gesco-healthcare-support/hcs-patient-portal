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
    private readonly ILogger<PacketGenerationOnApprovedHandler> _logger;

    public PacketGenerationOnApprovedHandler(
        IBackgroundJobManager backgroundJobManager,
        ILogger<PacketGenerationOnApprovedHandler> logger)
    {
        _backgroundJobManager = backgroundJobManager;
        _logger = logger;
    }

    [UnitOfWork]
    public virtual async Task HandleEventAsync(AppointmentStatusChangedEto eventData)
    {
        if (eventData.ToStatus != AppointmentStatusType.Approved)
        {
            return;
        }

        await _backgroundJobManager.EnqueueAsync(new GenerateAppointmentPacketArgs
        {
            AppointmentId = eventData.AppointmentId,
            TenantId = eventData.TenantId,
        });
        _logger.LogInformation(
            "PacketGenerationOnApprovedHandler: enqueued packet job for appointment {AppointmentId} (tenant {TenantId}).",
            eventData.AppointmentId, eventData.TenantId);
    }
}
