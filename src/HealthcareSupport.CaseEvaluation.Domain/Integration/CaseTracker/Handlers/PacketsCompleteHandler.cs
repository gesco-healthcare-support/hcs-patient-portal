using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus;
using Volo.Abp.Uow;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker.Handlers;

/// <summary>
/// Publishes an appointment's packets to the Case Tracker once ALL kinds have rendered.
///
/// <para>Stateless by design: the packet rows ARE the aggregator. Each
/// <see cref="PacketGeneratedEto"/> asks "is the set complete now?", so the first two events publish
/// nothing and the third publishes all three. That avoids an in-memory tally that a worker restart
/// would lose, and it means a regenerated packet re-publishes the set for free.</para>
///
/// <para>A fourth event for an already-complete set is harmless: the entry set is unchanged, so the
/// outbox's idempotency key matches the existing row and the enqueue collapses.</para>
/// </summary>
public class PacketsCompleteHandler :
    ILocalEventHandler<PacketGeneratedEto>,
    ITransientDependency
{
    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly IRepository<AppointmentPacket, Guid> _packetRepository;
    private readonly IDocumentListResolver _documentListResolver;
    private readonly ICaseTrackerDocumentQueue _documentQueue;
    private readonly ILogger<PacketsCompleteHandler> _logger;

    public PacketsCompleteHandler(
        IRepository<Appointment, Guid> appointmentRepository,
        IRepository<AppointmentPacket, Guid> packetRepository,
        IDocumentListResolver documentListResolver,
        ICaseTrackerDocumentQueue documentQueue,
        ILogger<PacketsCompleteHandler> logger)
    {
        _appointmentRepository = appointmentRepository;
        _packetRepository = packetRepository;
        _documentListResolver = documentListResolver;
        _documentQueue = documentQueue;
        _logger = logger;
    }

    [UnitOfWork]
    public virtual async Task HandleEventAsync(PacketGeneratedEto eventData)
    {
        if (eventData == null)
        {
            return;
        }

        try
        {
            var appointment = await _appointmentRepository.FindAsync(eventData.AppointmentId);
            if (appointment == null)
            {
                _logger.LogWarning(
                    "PacketsCompleteHandler: appointment {AppointmentId} not found; packets not published.",
                    eventData.AppointmentId);
                return;
            }

            if (!CaseTrackerPublishPolicy.IsPublished(appointment.AppointmentStatus))
            {
                _logger.LogDebug(
                    "PacketsCompleteHandler: appointment {AppointmentId} is {Status}; packets not published.",
                    eventData.AppointmentId, appointment.AppointmentStatus);
                return;
            }

            var packets = await _packetRepository.GetListAsync(p => p.AppointmentId == eventData.AppointmentId);
            if (!PacketSetPolicy.IsComplete(packets))
            {
                _logger.LogDebug(
                    "PacketsCompleteHandler: appointment {AppointmentId} has {Generated} of {Expected} packets generated; waiting for the rest.",
                    eventData.AppointmentId,
                    packets.Count(p => p.Status == PacketGenerationStatus.Generated),
                    PacketSetPolicy.AllKinds.Count);
                return;
            }

            var entries = await _documentListResolver.ResolvePacketsAsync(appointment);
            var row = await _documentQueue.EnqueueDocumentEntriesAsync(
                appointment.Id, appointment.TenantId, entries);

            _logger.LogInformation(
                "PacketsCompleteHandler: appointment {AppointmentId} packet set ({Count} entries) queued for Case Tracker (row {RowId}).",
                eventData.AppointmentId, entries.Count, row?.Id);
        }
        catch (Exception ex)
        {
            // Packet generation itself has already succeeded; losing the push must not fail the job.
            // The reconciliation sweep re-drives whatever this loses.
            _logger.LogError(
                ex,
                "PacketsCompleteHandler: failed to queue packets for appointment {AppointmentId}.",
                eventData.AppointmentId);
        }
    }
}
