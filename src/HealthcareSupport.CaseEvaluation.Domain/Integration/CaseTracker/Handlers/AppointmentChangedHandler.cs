using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Patients;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities.Events;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker.Handlers;

/// <summary>
/// Re-pushes an appointment whenever the data the Case Tracker holds about it changes.
///
/// <para>This REVERSES the earlier "field edits are pull-only" design. Relying on the receiver to
/// sweep every appointment periodically for freshness was overhead their team objected to, and the
/// idempotent enqueue makes re-pushing nearly free: an unchanged save produces the same
/// <c>updatedAt</c>, so the same idempotency key, so no second push.</para>
///
/// <para>Gated on <see cref="IntakeSettlePolicy"/> since 2026-07-30, and that gate is what makes
/// deferring the intake actually work. Approval IS an appointment update, so this handler fires on the
/// approval itself and would push a packet-less intake even after the approval trigger was removed --
/// the two enqueues simply collapsed onto one row because they shared an <c>updatedAt</c>, which is why
/// the first live approval produced two rows rather than three. Skipping while the packet set is still
/// rendering leaves the settle path to send one complete message; a genuine later edit arrives after
/// the set is complete, so it still re-pushes immediately.</para>
///
/// <para>Watches <see cref="Appointment"/> and <see cref="Patient"/> ONLY. The payload carries
/// appointment scalars, tenant, location, appointment type, schedule, patient, doctor, storage and
/// documents -- it carries NO attorney, injury, employer or insurance fields, so edits to those cannot
/// change what we publish. <c>Location</c>, <c>Doctor</c> and <c>AppointmentType</c> DO appear in the
/// payload but are rare admin edits that fan out to every appointment at that location, so they are
/// deliberately excluded (confirmed 2026-07-28). ACCEPTED CONSEQUENCE: renaming a clinic does not
/// immediately refresh cases already in the Case Tracker; they pick it up via reconcile-on-open.</para>
/// </summary>
public class AppointmentChangedHandler :
    ILocalEventHandler<EntityUpdatedEventData<Appointment>>,
    ILocalEventHandler<EntityUpdatedEventData<Patient>>,
    ITransientDependency
{
    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly IRepository<AppointmentPacket, Guid> _packetRepository;
    private readonly ICaseTrackerIntakeQueue _intakeQueue;
    private readonly IClock _clock;
    private readonly ILogger<AppointmentChangedHandler> _logger;

    public AppointmentChangedHandler(
        IRepository<Appointment, Guid> appointmentRepository,
        IRepository<AppointmentPacket, Guid> packetRepository,
        ICaseTrackerIntakeQueue intakeQueue,
        IClock clock,
        ILogger<AppointmentChangedHandler> logger)
    {
        _appointmentRepository = appointmentRepository;
        _packetRepository = packetRepository;
        _intakeQueue = intakeQueue;
        _clock = clock;
        _logger = logger;
    }

    [UnitOfWork]
    public virtual async Task HandleEventAsync(EntityUpdatedEventData<Appointment> eventData)
    {
        var appointment = eventData?.Entity;
        if (appointment == null)
        {
            return;
        }

        if (!CaseTrackerPublishPolicy.ShouldPublish(appointment.AppointmentStatus))
        {
            // Either not yet a case on their side -- the settle path pushes the current state in full
            // once the appointment is published and its packets have rendered -- or closed by an
            // attendance outcome THEY reported, which we never echo back (phase 5).
            _logger.LogDebug(
                "AppointmentChangedHandler: appointment {AppointmentId} is {Status}; nothing pushed.",
                appointment.Id, appointment.AppointmentStatus);
            return;
        }

        await RePushAsync(appointment.Id, appointment.TenantId, nameof(Appointment));
    }

    [UnitOfWork]
    public virtual async Task HandleEventAsync(EntityUpdatedEventData<Patient> eventData)
    {
        var patient = eventData?.Entity;
        if (patient == null)
        {
            return;
        }

        try
        {
            // Demographics appear in every one of this patient's payloads, so each published
            // appointment needs the correction -- not just the most recent one.
            var appointments = await _appointmentRepository.GetListAsync(a => a.PatientId == patient.Id);

            foreach (var appointment in appointments)
            {
                if (CaseTrackerPublishPolicy.ShouldPublish(appointment.AppointmentStatus))
                {
                    await RePushAsync(appointment.Id, appointment.TenantId, nameof(Patient));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "AppointmentChangedHandler: failed to re-push appointments for patient {PatientId}; the edit stands.",
                patient.Id);
        }
    }

    private async Task RePushAsync(Guid appointmentId, Guid? tenantId, string trigger)
    {
        try
        {
            var appointment = await _appointmentRepository.FindAsync(appointmentId);
            if (appointment == null)
            {
                return;
            }

            var packets = await _packetRepository.GetListAsync(p => p.AppointmentId == appointmentId);
            if (!IntakeSettlePolicy.IsSettled(appointment, packets, PacketSetPolicy.Cutoff(_clock.Now)))
            {
                // Mid-render. Pushing now would send the packet-less version that the settle path is
                // about to supersede -- the exact double push this gate exists to remove.
                _logger.LogDebug(
                    "AppointmentChangedHandler: {Trigger} change for appointment {AppointmentId} skipped; its packet set has not settled yet.",
                    trigger, appointmentId);
                return;
            }

            var row = await _intakeQueue.EnqueueIntakeAsync(appointmentId, tenantId);

            _logger.LogDebug(
                "AppointmentChangedHandler: {Trigger} change queued a re-push for appointment {AppointmentId} (row {RowId}).",
                trigger, appointmentId, row.Id);
        }
        catch (Exception ex)
        {
            // The edit is the primary business action. A lost re-push leaves the receiver holding
            // slightly stale data, which reconcile-on-open corrects.
            _logger.LogError(
                ex,
                "AppointmentChangedHandler: failed to queue a re-push for appointment {AppointmentId} after a {Trigger} change; the edit stands.",
                appointmentId, trigger);
        }
    }
}
