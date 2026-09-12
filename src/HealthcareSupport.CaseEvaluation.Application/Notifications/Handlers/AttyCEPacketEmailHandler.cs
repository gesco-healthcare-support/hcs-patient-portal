using System;
using System.Collections.Generic;
using System.Linq;
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
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Settings;
using Volo.Abp.Uow;

namespace HealthcareSupport.CaseEvaluation.Notifications.Handlers;

/// <summary>
/// Phase 4 (Category 4, 2026-05-10) -- subscribes to
/// <see cref="PacketGeneratedEto"/> filtered to
/// <see cref="PacketKind.AttorneyClaimExaminer"/> and fans the OLD-parity
/// <c>AppointmentDocumentAddWithAttachment</c> email out to every active
/// Applicant Attorney + Defense Attorney + Claim Examiner on the
/// appointment, each with the rendered AttyCE DOCX attached.
///
/// <para>Mirrors OLD <c>AppointmentDocumentDomain.cs</c>:636-859 -- the
/// per-recipient email fan-out for Adjuster / Claim Examiner / Defense
/// Attorney / Patient Attorney (all using the same
/// <c>aws.attorneyClaimExaminer</c> template). Single template
/// per Adrian Decision 2026-05-10 (matches current OLD code, not the
/// per-PQME/AME folder split that exists on OLD's disk but is never
/// read by C#).</para>
///
/// <para>The job already only generates the AttyCE kind for PQME /
/// PQMEREEVAL / AME / AMEREEVAL appointment types (see
/// <c>GenerateAppointmentPacketJob.AttorneyClaimExaminerTypes</c>), so
/// this handler does not need to re-check the type -- if the event
/// fires for this kind, the type is in scope by construction.</para>
///
/// <para>After each successful send the
/// <c>SendAppointmentEmailJob</c> calls
/// <see cref="IPacketAttachmentProvider.NotifySendCompletedAsync"/>;
/// the provider's retention rule prunes the AttyCE row + blob once any
/// recipient receives the attachment. On the first per-recipient failure
/// the row stays so the office can re-trigger via the Regenerate path.
/// MVP accepts a race where multiple successful AttyCE sends each try to
/// prune (the second delete is a no-op after the entity is gone).</para>
/// </summary>
public class AttyCEPacketEmailHandler :
    ILocalEventHandler<PacketGeneratedEto>,
    ITransientDependency
{
    private static readonly RecipientRole[] AttyCERoles =
    {
        RecipientRole.ApplicantAttorney,
        RecipientRole.DefenseAttorney,
        RecipientRole.ClaimExaminer,
    };

    private readonly INotificationDispatcher _dispatcher;
    private readonly DocumentEmailContextResolver _contextResolver;
    private readonly IAppointmentRecipientResolver _recipientResolver;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<AttyCEPacketEmailHandler> _logger;
    // BUG-029 v3 fix (2026-05-21).
    private readonly IAccountUrlBuilder _accountUrlBuilder;
    // E5 (2026-06-09): resolve each recipient's display name for the greeting.
    private readonly IdentityUserManager _userManager;

    public AttyCEPacketEmailHandler(
        INotificationDispatcher dispatcher,
        DocumentEmailContextResolver contextResolver,
        IAppointmentRecipientResolver recipientResolver,
        ICurrentTenant currentTenant,
        ILogger<AttyCEPacketEmailHandler> logger,
        IAccountUrlBuilder accountUrlBuilder,
        IdentityUserManager userManager)
    {
        _dispatcher = dispatcher;
        _contextResolver = contextResolver;
        _recipientResolver = recipientResolver;
        _currentTenant = currentTenant;
        _logger = logger;
        _accountUrlBuilder = accountUrlBuilder;
        _userManager = userManager;
    }

    [UnitOfWork]
    public virtual async Task HandleEventAsync(PacketGeneratedEto eventData)
    {
        if (eventData == null || eventData.Kind != PacketKind.AttorneyClaimExaminer)
        {
            return;
        }

        using (_currentTenant.Change(eventData.TenantId))
        {
            var ctx = await _contextResolver.ResolveAsync(eventData.AppointmentId, appointmentDocumentId: null);
            if (ctx == null)
            {
                _logger.LogWarning(
                    "AttyCEPacketEmailHandler: appointment {AppointmentId} not found; skipping.",
                    eventData.AppointmentId);
                return;
            }

            var resolverOutput = await _recipientResolver.ResolveAsync(
                eventData.AppointmentId,
                NotificationKind.PacketAttyCEDelivery);

            var recipients = resolverOutput
                .Where(r => !string.IsNullOrWhiteSpace(r.To) && r.Role.HasValue && AttyCERoles.Contains(r.Role.Value))
                .Select(r => new NotificationRecipient(
                    email: r.To,
                    role: r.Role,
                    isRegistered: r.IsRegistered))
                .ToList();

            if (recipients.Count == 0)
            {
                _logger.LogInformation(
                    "AttyCEPacketEmailHandler: no AA/DA/CE recipients for appointment {AppointmentId}; skipping (no email but packet row remains for manual re-send).",
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

            // E5 (2026-06-09): send per recipient so each Attorney/CE is greeted
            // by name. The recipient resolver returns email + role only, so the
            // name is resolved from the IdentityUser; unregistered recipients
            // (no account yet) fall back to a neutral greeting. PacketLabel
            // drives the "Appointment Notice" subject (vs the patient's
            // "Patient Packet"). One email job per recipient -- same count as
            // before, so the packet-prune retention behaviour is unchanged.
            foreach (var recipient in recipients)
            {
                var user = await _userManager.FindByEmailAsync(recipient.Email);
                var recipientName = $"{user?.Name} {user?.Surname}".Trim();
                var greeting = string.IsNullOrWhiteSpace(recipientName) ? "Hello," : $"Hello {recipientName},";

                var withUrl = new Dictionary<string, object?>(variables, StringComparer.Ordinal)
                {
                    ["URL"] = documentUploadUrl,
                    ["PacketLabel"] = "Appointment Notice",
                    ["Greeting"] = greeting,
                };

                await _dispatcher.DispatchAsync(
                    templateCode: NotificationTemplateConsts.Codes.AppointmentDocumentAddWithAttachment,
                    recipients: new[] { recipient },
                    variables: withUrl,
                    contextTag: $"AttyCEPacket/{eventData.AppointmentId}",
                    packetRef: new PacketAttachmentRef
                    {
                        AppointmentId = eventData.AppointmentId,
                        PacketId = eventData.PacketId,
                        Kind = PacketKind.AttorneyClaimExaminer,
                    });
            }
        }
    }
}
