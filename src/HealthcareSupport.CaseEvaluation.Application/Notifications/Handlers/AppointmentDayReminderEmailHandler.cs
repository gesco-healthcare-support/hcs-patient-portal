using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.DoctorAvailabilities;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace HealthcareSupport.CaseEvaluation.Notifications.Handlers;

/// <summary>
/// 2026-11-03 (email routing, row 15) -- the appointment reminder, 7 days and 1 day before an Approved
/// appointment. Subscribes to <see cref="AppointmentDayReminderEto"/> (raised by
/// <c>AppointmentDayReminderJob</c>) and sends the editable <c>AppointmentDayReminder</c> template through
/// the dispatcher and outbox to the patient, the office inbox and the switched-on Intake Staff. Attorneys,
/// the claim examiner and Staff Supervisors are not recipients.
///
/// <para>Only an appointment that is STILL Approved gets one (it may have been cancelled between the job and
/// this handler), and a patient whose login has been switched off is not mailed. Each offset has its own
/// context tag, so the 7-day and the 1-day reminder each send once.</para>
/// </summary>
public class AppointmentDayReminderEmailHandler :
    ILocalEventHandler<AppointmentDayReminderEto>,
    ITransientDependency
{
    private readonly INotificationDispatcher _dispatcher;
    private readonly DocumentEmailContextResolver _contextResolver;
    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly IRepository<DoctorAvailability, Guid> _slotRepository;
    private readonly IRepository<IdentityUser, Guid> _identityUserRepository;
    private readonly OfficeNotificationRecipients _officeRecipients;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<AppointmentDayReminderEmailHandler> _logger;

    public AppointmentDayReminderEmailHandler(
        INotificationDispatcher dispatcher,
        DocumentEmailContextResolver contextResolver,
        IRepository<Appointment, Guid> appointmentRepository,
        IRepository<DoctorAvailability, Guid> slotRepository,
        IRepository<IdentityUser, Guid> identityUserRepository,
        OfficeNotificationRecipients officeRecipients,
        ICurrentTenant currentTenant,
        ILogger<AppointmentDayReminderEmailHandler> logger)
    {
        _dispatcher = dispatcher;
        _contextResolver = contextResolver;
        _appointmentRepository = appointmentRepository;
        _slotRepository = slotRepository;
        _identityUserRepository = identityUserRepository;
        _officeRecipients = officeRecipients;
        _currentTenant = currentTenant;
        _logger = logger;
    }

    [UnitOfWork]
    public virtual async Task HandleEventAsync(AppointmentDayReminderEto eventData)
    {
        if (eventData == null)
        {
            return;
        }

        using (_currentTenant.Change(eventData.TenantId))
        {
            var appointment = await _appointmentRepository.FindAsync(eventData.AppointmentId);
            if (appointment == null || appointment.AppointmentStatus != AppointmentStatusType.Approved)
            {
                _logger.LogInformation(
                    "AppointmentDayReminderEmailHandler: appointment {AppointmentId} is missing or no longer Approved; no reminder.",
                    eventData.AppointmentId);
                return;
            }

            var ctx = await _contextResolver.ResolveAsync(eventData.AppointmentId, appointmentDocumentId: null);
            if (ctx == null)
            {
                return;
            }

            var recipients = new List<NotificationRecipient>();
            if (await PatientIsMailableAsync(ctx))
            {
                recipients.Add(new NotificationRecipient(
                    email: ctx.PatientEmail!, role: RecipientRole.Patient, isRegistered: ctx.PatientIdentityUserId.HasValue));
            }
            recipients.AddRange(await _officeRecipients.GetOfficeInboxAndIntakeAsync(
                $"AppointmentDayReminder/{eventData.AppointmentId}"));
            recipients = OfficeNotificationRecipients.Distinct(recipients);
            if (recipients.Count == 0)
            {
                return;
            }

            var slot = await _slotRepository.FindAsync(appointment.DoctorAvailabilityId);
            var vars = new Dictionary<string, object?>(
                DocumentNotificationContext.BuildVariables(
                    patientFirstName: ctx.PatientFirstName,
                    patientLastName: ctx.PatientLastName,
                    patientEmail: ctx.PatientEmail,
                    requestConfirmationNumber: ctx.RequestConfirmationNumber,
                    appointmentDate: appointment.AppointmentDate,
                    claimNumber: ctx.ClaimNumber,
                    wcabAdj: ctx.WcabAdj,
                    documentName: null,
                    rejectionNotes: null,
                    clinicName: null,
                    portalUrl: ctx.PortalBaseUrl),
                StringComparer.Ordinal)
            {
                ["AppointmentFromTime"] = slot == null
                    ? string.Empty
                    : slot.FromTime.ToString("h:mm tt", CultureInfo.GetCultureInfo("en-US")),
                ["ReminderWhen"] = eventData.DaysUntil == 1 ? "tomorrow" : $"in {eventData.DaysUntil} days",
            };

            try
            {
                await _dispatcher.DispatchAsync(
                    templateCode: NotificationTemplateConsts.Codes.AppointmentDayReminder,
                    recipients: recipients,
                    variables: vars,
                    contextTag: $"AppointmentDayReminder/T-{eventData.DaysUntil}/{eventData.AppointmentId}");
            }
            catch (BusinessException ex)
                when (ex.Code == CaseEvaluationDomainErrorCodes.NotificationTemplateNotFound)
            {
                _logger.LogWarning(
                    ex,
                    "AppointmentDayReminderEmailHandler: template missing/inactive; reminder skipped for appointment {AppointmentId}.",
                    eventData.AppointmentId);
            }
        }
    }

    /// <summary>The patient has an address, and no switched-off login of their own.</summary>
    private async Task<bool> PatientIsMailableAsync(DocumentEmailContext ctx)
    {
        if (string.IsNullOrWhiteSpace(ctx.PatientEmail))
        {
            return false;
        }

        if (ctx.PatientIdentityUserId is { } login)
        {
            var user = await _identityUserRepository.FindAsync(login);
            if (user != null && !user.IsActive)
            {
                return false;
            }
        }
        return true;
    }
}
