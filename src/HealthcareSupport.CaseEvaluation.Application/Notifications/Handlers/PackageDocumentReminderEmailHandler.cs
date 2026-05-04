using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace HealthcareSupport.CaseEvaluation.Notifications.Handlers;

/// <summary>
/// Phase 14b (2026-05-04) -- subscribes to
/// <see cref="PackageDocumentReminderEto"/> and dispatches the
/// OLD-parity <c>PackageDocumentsReminder</c> /
/// <c>JDFReminder</c> email to the patient (or the document's
/// original uploader).
///
/// <para>JDF documents get the
/// <see cref="NotificationTemplateConsts.Codes.JDFReminder"/>
/// template; package docs get
/// <see cref="NotificationTemplateConsts.Codes.PackageDocumentsReminder"/>.
/// Mirrors OLD's two separate reminder templates per spec lines
/// 569-593 -- "Reminder for incomplete package documents (multiple
/// reminders)" + "JDFReminder".</para>
/// </summary>
public class PackageDocumentReminderEmailHandler :
    ILocalEventHandler<PackageDocumentReminderEto>,
    ITransientDependency
{
    private readonly INotificationDispatcher _dispatcher;
    private readonly DocumentEmailContextResolver _contextResolver;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<PackageDocumentReminderEmailHandler> _logger;

    public PackageDocumentReminderEmailHandler(
        INotificationDispatcher dispatcher,
        DocumentEmailContextResolver contextResolver,
        ICurrentTenant currentTenant,
        ILogger<PackageDocumentReminderEmailHandler> logger)
    {
        _dispatcher = dispatcher;
        _contextResolver = contextResolver;
        _currentTenant = currentTenant;
        _logger = logger;
    }

    [UnitOfWork]
    public virtual async Task HandleEventAsync(PackageDocumentReminderEto eventData)
    {
        if (eventData == null)
        {
            return;
        }

        using (_currentTenant.Change(eventData.TenantId))
        {
            var ctx = await _contextResolver.ResolveAsync(eventData.AppointmentId, eventData.AppointmentDocumentId);
            if (ctx == null)
            {
                _logger.LogWarning(
                    "PackageDocumentReminderEmailHandler: appointment {AppointmentId} not found; skipping.",
                    eventData.AppointmentId);
                return;
            }

            var uploaderEmail = await _contextResolver.ResolveUploaderEmailAsync(
                ctx.DocumentUploadedByUserId,
                ctx.PatientEmail ?? ctx.BookerEmail);

            if (string.IsNullOrWhiteSpace(uploaderEmail))
            {
                return;
            }

            var recipients = new List<NotificationRecipient>
            {
                new NotificationRecipient(
                    email: uploaderEmail!,
                    role: RecipientRole.Patient,
                    isRegistered: ctx.DocumentUploadedByUserId.HasValue),
            };

            var variables = DocumentNotificationContext.BuildVariables(
                patientFirstName: ctx.PatientFirstName,
                patientLastName: ctx.PatientLastName,
                patientEmail: ctx.PatientEmail,
                requestConfirmationNumber: ctx.RequestConfirmationNumber,
                appointmentDate: ctx.AppointmentDate,
                claimNumber: ctx.ClaimNumber,
                wcabAdj: ctx.WcabAdj,
                documentName: ctx.DocumentName,
                rejectionNotes: null,
                clinicName: _currentTenant.Name,
                portalUrl: ctx.PortalBaseUrl);

            var templateCode = eventData.IsJointDeclaration
                ? NotificationTemplateConsts.Codes.JDFReminder
                : NotificationTemplateConsts.Codes.PackageDocumentsReminder;

            await _dispatcher.DispatchAsync(
                templateCode: templateCode,
                recipients: recipients,
                variables: variables,
                contextTag: $"PackageDocumentReminder/{eventData.AppointmentDocumentId}");
        }
    }
}
