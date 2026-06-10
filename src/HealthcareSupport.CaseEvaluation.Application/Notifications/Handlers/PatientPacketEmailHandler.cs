using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using HealthcareSupport.CaseEvaluation.Settings;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Settings;
using Volo.Abp.Uow;

namespace HealthcareSupport.CaseEvaluation.Notifications.Handlers;

/// <summary>
/// Phase 4 (Category 4, 2026-05-10) -- subscribes to
/// <see cref="PacketGeneratedEto"/> filtered to
/// <see cref="PacketKind.Patient"/> and dispatches the OLD-parity
/// <c>AppointmentDocumentAddWithAttachment</c> template to the patient
/// with the rendered DOCX attached.
///
/// <para>Mirrors OLD <c>AppointmentDocumentDomain.cs</c>:463-550 -- the
/// Patient packet leg. Single recipient: <c>appointment.Patient.Email</c>
/// (skip if null per OLD's silent-skip behavior at :467). Doctor packet
/// has no email path so the analogous handler does not exist.</para>
/// </summary>
public class PatientPacketEmailHandler :
    ILocalEventHandler<PacketGeneratedEto>,
    ITransientDependency
{
    private readonly INotificationDispatcher _dispatcher;
    private readonly DocumentEmailContextResolver _contextResolver;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<PatientPacketEmailHandler> _logger;
    // BUG-029 v3 fix (2026-05-21).
    private readonly IAccountUrlBuilder _accountUrlBuilder;

    public PatientPacketEmailHandler(
        INotificationDispatcher dispatcher,
        DocumentEmailContextResolver contextResolver,
        ICurrentTenant currentTenant,
        ILogger<PatientPacketEmailHandler> logger,
        IAccountUrlBuilder accountUrlBuilder)
    {
        _dispatcher = dispatcher;
        _contextResolver = contextResolver;
        _currentTenant = currentTenant;
        _logger = logger;
        _accountUrlBuilder = accountUrlBuilder;
    }

    [UnitOfWork]
    public virtual async Task HandleEventAsync(PacketGeneratedEto eventData)
    {
        if (eventData == null || eventData.Kind != PacketKind.Patient)
        {
            return;
        }

        using (_currentTenant.Change(eventData.TenantId))
        {
            var ctx = await _contextResolver.ResolveAsync(eventData.AppointmentId, appointmentDocumentId: null);
            if (ctx == null)
            {
                _logger.LogWarning(
                    "PatientPacketEmailHandler: appointment {AppointmentId} not found; skipping.",
                    eventData.AppointmentId);
                return;
            }

            if (string.IsNullOrWhiteSpace(ctx.PatientEmail))
            {
                _logger.LogInformation(
                    "PatientPacketEmailHandler: patient email empty for appointment {AppointmentId}; skipping (OLD silent-skip parity).",
                    eventData.AppointmentId);
                return;
            }

            // BUG-029 v3 fix (2026-05-21).
            var portalUrl = ctx.PortalBaseUrl
                ?? await _accountUrlBuilder.BuildPortalRootUrlAsync(eventData.TenantId);

            var documentUploadUrl = $"{portalUrl.TrimEnd('/')}/appointments/view/{eventData.AppointmentId:N}";

            var variables = DocumentNotificationContext.BuildVariables(
                patientFirstName: ctx.PatientFirstName,
                patientLastName: ctx.PatientLastName,
                patientEmail: ctx.PatientEmail,
                requestConfirmationNumber: ctx.RequestConfirmationNumber,
                appointmentDate: ctx.AppointmentDate,
                claimNumber: ctx.ClaimNumber,
                wcabAdj: ctx.WcabAdj,
                documentName: null,
                rejectionNotes: null,
                clinicName: _currentTenant.Name,
                portalUrl: documentUploadUrl);

            // Override the PortalUrl key with the documents-upload URL.
            // DocumentNotificationContext.BuildVariables maps portalUrl to
            // the "PortalUrl" key, but OLD's body uses "##URL##" pointing
            // to the per-appointment documents page. We add both keys so
            // the template can use either name.
            var patientName = $"{ctx.PatientFirstName} {ctx.PatientLastName}".Trim();
            var withUrl = new Dictionary<string, object?>(variables, StringComparer.Ordinal)
            {
                ["URL"] = documentUploadUrl,
                ["PacketLabel"] = "Patient Packet",
                ["Greeting"] = string.IsNullOrWhiteSpace(patientName) ? "Hello," : $"Hello {patientName},",
            };

            var recipients = new List<NotificationRecipient>
            {
                new NotificationRecipient(
                    email: ctx.PatientEmail!,
                    role: RecipientRole.Patient,
                    isRegistered: true),
            };

            await _dispatcher.DispatchAsync(
                templateCode: NotificationTemplateConsts.Codes.AppointmentDocumentAddWithAttachment,
                recipients: recipients,
                variables: withUrl,
                contextTag: $"PatientPacket/{eventData.AppointmentId}",
                packetRef: new PacketAttachmentRef
                {
                    AppointmentId = eventData.AppointmentId,
                    PacketId = eventData.PacketId,
                    Kind = PacketKind.Patient,
                });
        }
    }
}
