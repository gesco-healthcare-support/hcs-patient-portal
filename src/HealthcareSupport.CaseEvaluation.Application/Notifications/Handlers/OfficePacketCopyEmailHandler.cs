using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.DoctorAvailabilities;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace HealthcareSupport.CaseEvaluation.Notifications.Handlers;

/// <summary>
/// The office's copy of an appointment's packets (2026-11-03): ONE email to the office notification inbox
/// carrying both the attorney / claim examiner packet and the patient packet. The doctor packet is never
/// attached, and Intake Staff and the parties are not recipients (each party still gets its own packet
/// from <see cref="PatientPacketEmailHandler"/> / <see cref="AttyCEPacketEmailHandler"/>, unchanged).
///
/// <para><b>Trigger.</b> Stateless, like <c>PacketsCompleteHandler</c>: every attorney / patient
/// <see cref="PacketGeneratedEto"/> asks "are BOTH of those packets Generated now?", so whichever packet
/// finishes second sends the copy and the first sends nothing. Every appointment type renders all three
/// kinds, so the only appointment without a copy is one whose attorney or patient packet is not Generated
/// (for example a render that failed for good); a staff Regenerate then raises the event that sends it.</para>
///
/// <para><b>Once per set.</b> The context tag carries the packet version -- the number of earlier files a
/// re-render has replaced for this appointment (<see cref="AppointmentPacketVersion"/>) -- so a repeat
/// event of the same set collapses in the outbox, while a re-render after a party change (a new file)
/// sends the office a new copy. A re-render that failed and kept the old file adds no version row, so it
/// sends nothing new.</para>
///
/// <para>No office inbox set: nothing is sent and a warning is logged
/// (<see cref="OfficeNotificationRecipients.GetOfficeInboxAsync"/>).</para>
/// </summary>
public class OfficePacketCopyEmailHandler :
    ILocalEventHandler<PacketGeneratedEto>,
    ITransientDependency
{
    private readonly IPacketCopyDispatcher _dispatcher;
    private readonly OfficeNotificationRecipients _officeRecipients;
    private readonly IRepository<AppointmentPacket, Guid> _packetRepository;
    private readonly IRepository<AppointmentPacketVersion, Guid> _versionRepository;
    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly IRepository<DoctorAvailability, Guid> _availabilityRepository;
    private readonly DocumentEmailContextResolver _contextResolver;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<OfficePacketCopyEmailHandler> _logger;

    public OfficePacketCopyEmailHandler(
        IPacketCopyDispatcher dispatcher,
        OfficeNotificationRecipients officeRecipients,
        IRepository<AppointmentPacket, Guid> packetRepository,
        IRepository<AppointmentPacketVersion, Guid> versionRepository,
        IRepository<Appointment, Guid> appointmentRepository,
        IRepository<DoctorAvailability, Guid> availabilityRepository,
        DocumentEmailContextResolver contextResolver,
        ICurrentTenant currentTenant,
        ILogger<OfficePacketCopyEmailHandler> logger)
    {
        _dispatcher = dispatcher;
        _officeRecipients = officeRecipients;
        _packetRepository = packetRepository;
        _versionRepository = versionRepository;
        _appointmentRepository = appointmentRepository;
        _availabilityRepository = availabilityRepository;
        _contextResolver = contextResolver;
        _currentTenant = currentTenant;
        _logger = logger;
    }

    [UnitOfWork]
    public virtual async Task HandleEventAsync(PacketGeneratedEto eventData)
    {
        if (eventData == null
            || (eventData.Kind != PacketKind.AttorneyClaimExaminer && eventData.Kind != PacketKind.Patient))
        {
            return;
        }

        using (_currentTenant.Change(eventData.TenantId))
        {
            var packets = await _packetRepository.GetListAsync(p => p.AppointmentId == eventData.AppointmentId);
            var attyCe = packets.FirstOrDefault(p =>
                p.Kind == PacketKind.AttorneyClaimExaminer && p.Status == PacketGenerationStatus.Generated);
            var patient = packets.FirstOrDefault(p =>
                p.Kind == PacketKind.Patient && p.Status == PacketGenerationStatus.Generated);
            if (attyCe == null || patient == null)
            {
                _logger.LogDebug(
                    "OfficePacketCopyEmailHandler: appointment {AppointmentId} has not generated both the attorney and the patient packet yet; waiting.",
                    eventData.AppointmentId);
                return;
            }

            var ctx = await _contextResolver.ResolveAsync(eventData.AppointmentId, appointmentDocumentId: null);
            if (ctx == null)
            {
                _logger.LogWarning(
                    "OfficePacketCopyEmailHandler: appointment {AppointmentId} not found; skipping.",
                    eventData.AppointmentId);
                return;
            }

            var inbox = await _officeRecipients.GetOfficeInboxAsync("OfficePacketCopy");
            if (inbox.Count == 0)
            {
                return; // GetOfficeInboxAsync has already logged the warning.
            }

            var version = await _versionRepository.CountAsync(v => v.AppointmentId == eventData.AppointmentId);

            var appointment = await _appointmentRepository.FindAsync(eventData.AppointmentId);
            var availability = appointment == null
                ? null
                : await _availabilityRepository.FindAsync(appointment.DoctorAvailabilityId);

            var variables = new Dictionary<string, object?>(
                DocumentNotificationContext.BuildVariables(
                    patientFirstName: ctx.PatientFirstName,
                    patientLastName: ctx.PatientLastName,
                    patientEmail: ctx.PatientEmail,
                    requestConfirmationNumber: ctx.RequestConfirmationNumber,
                    appointmentDate: ctx.AppointmentDate,
                    claimNumber: ctx.ClaimNumber,
                    wcabAdj: ctx.WcabAdj,
                    documentName: null,
                    rejectionNotes: null,
                    // Filled by NotificationTemplateRenderer from the tenant store (#1014).
                    clinicName: null,
                    portalUrl: null),
                StringComparer.Ordinal)
            {
                ["AppointmentFromTime"] = availability?.FromTime is { } fromTime
                    ? fromTime.ToString("hh:mm tt", CultureInfo.GetCultureInfo("en-US"))
                    : string.Empty,
            };

            await _dispatcher.DispatchWithPacketsAsync(
                notificationCase: NotificationCase.Of(eventData.AppointmentId),
                templateCode: NotificationTemplateConsts.Codes.OfficePacketCopy,
                recipients: inbox,
                variables: variables,
                contextTag: $"OfficePacketCopy/{eventData.AppointmentId}/v{version}",
                packetRefs: new[]
                {
                    new PacketAttachmentRef
                    {
                        AppointmentId = eventData.AppointmentId,
                        PacketId = attyCe.Id,
                        Kind = PacketKind.AttorneyClaimExaminer,
                    },
                    new PacketAttachmentRef
                    {
                        AppointmentId = eventData.AppointmentId,
                        PacketId = patient.Id,
                        Kind = PacketKind.Patient,
                    },
                });
        }
    }
}
