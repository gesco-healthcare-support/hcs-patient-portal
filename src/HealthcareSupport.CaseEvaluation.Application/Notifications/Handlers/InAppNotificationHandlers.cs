using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace HealthcareSupport.CaseEvaluation.Notifications.Handlers;

/// <summary>
/// QA item 7: the parallel in-app notification handlers. They subscribe to the
/// SAME inbound ETOs the email handlers use, but create in-app notifications for
/// the office's INTERNAL staff (a different audience) via
/// <see cref="AppNotificationManager"/>. Kept separate from the email handlers so
/// the email flow is untouched and either side can fail independently. Each runs
/// inside the event's tenant scope so the manager resolves that office's staff and
/// stamps TenantId. A relative SPA deep-link (<c>/appointments/view/{id}</c>) sends
/// the recipient to the case; bodies carry the confirmation number + generic
/// phrasing (no patient PHI beyond the queue).
/// </summary>
internal static class InAppNotificationUrls
{
    public static string AppointmentDetail(Guid appointmentId) => $"/appointments/view/{appointmentId}";
}

/// <summary>New appointment request submitted -> notify office staff to review it.</summary>
public class AppointmentSubmittedInAppNotificationHandler :
    ILocalEventHandler<AppointmentSubmittedEto>,
    ITransientDependency
{
    private readonly AppNotificationManager _notificationManager;
    private readonly ICurrentTenant _currentTenant;

    public AppointmentSubmittedInAppNotificationHandler(
        AppNotificationManager notificationManager, ICurrentTenant currentTenant)
    {
        _notificationManager = notificationManager;
        _currentTenant = currentTenant;
    }

    [UnitOfWork]
    public virtual async Task HandleEventAsync(AppointmentSubmittedEto eventData)
    {
        if (eventData == null)
        {
            return;
        }
        using (_currentTenant.Change(eventData.TenantId))
        {
            await _notificationManager.RaiseForOfficeStaffAsync(
                AppNotificationType.AppointmentRequested,
                title: "New appointment request",
                body: $"Request {eventData.RequestConfirmationNumber} was submitted and needs review.",
                url: InAppNotificationUrls.AppointmentDetail(eventData.AppointmentId));
        }
    }
}

/// <summary>Reschedule / cancel change request submitted -> notify office staff to decide.</summary>
public class ChangeRequestSubmittedInAppNotificationHandler :
    ILocalEventHandler<AppointmentChangeRequestSubmittedEto>,
    ITransientDependency
{
    private readonly AppNotificationManager _notificationManager;
    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly ICurrentTenant _currentTenant;

    public ChangeRequestSubmittedInAppNotificationHandler(
        AppNotificationManager notificationManager,
        IRepository<Appointment, Guid> appointmentRepository,
        ICurrentTenant currentTenant)
    {
        _notificationManager = notificationManager;
        _appointmentRepository = appointmentRepository;
        _currentTenant = currentTenant;
    }

    [UnitOfWork]
    public virtual async Task HandleEventAsync(AppointmentChangeRequestSubmittedEto eventData)
    {
        if (eventData == null)
        {
            return;
        }
        using (_currentTenant.Change(eventData.TenantId))
        {
            var isCancel = eventData.ChangeRequestType == ChangeRequestType.Cancel;
            var confirmation = await ResolveConfirmationAsync(eventData.AppointmentId);
            var action = isCancel ? "Cancellation" : "Reschedule";
            await _notificationManager.RaiseForOfficeStaffAsync(
                AppNotificationType.ChangeRequestSubmitted,
                title: $"{action} request",
                body: $"A {action.ToLowerInvariant()} request was submitted for {confirmation} and needs a decision.",
                url: InAppNotificationUrls.AppointmentDetail(eventData.AppointmentId));
        }
    }

    private async Task<string> ResolveConfirmationAsync(Guid appointmentId)
    {
        var appointment = await _appointmentRepository.FindAsync(appointmentId);
        return appointment?.RequestConfirmationNumber ?? "an appointment";
    }
}

/// <summary>User question submitted -> notify office staff.</summary>
public class UserQuerySubmittedInAppNotificationHandler :
    ILocalEventHandler<UserQuerySubmittedEto>,
    ITransientDependency
{
    private readonly AppNotificationManager _notificationManager;
    private readonly ICurrentTenant _currentTenant;

    public UserQuerySubmittedInAppNotificationHandler(
        AppNotificationManager notificationManager, ICurrentTenant currentTenant)
    {
        _notificationManager = notificationManager;
        _currentTenant = currentTenant;
    }

    [UnitOfWork]
    public virtual async Task HandleEventAsync(UserQuerySubmittedEto eventData)
    {
        if (eventData == null)
        {
            return;
        }
        using (_currentTenant.Change(eventData.TenantId))
        {
            var about = string.IsNullOrWhiteSpace(eventData.RequestConfirmationNumber)
                ? "A user submitted a question."
                : $"A user submitted a question about {eventData.RequestConfirmationNumber}.";
            await _notificationManager.RaiseForOfficeStaffAsync(
                AppNotificationType.QuerySubmitted,
                title: "New question",
                body: about,
                url: null);
        }
    }
}

/// <summary>Document uploaded against an appointment -> notify office staff.</summary>
public class DocumentUploadedInAppNotificationHandler :
    ILocalEventHandler<AppointmentDocumentUploadedEto>,
    ITransientDependency
{
    private readonly AppNotificationManager _notificationManager;
    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly ICurrentTenant _currentTenant;

    public DocumentUploadedInAppNotificationHandler(
        AppNotificationManager notificationManager,
        IRepository<Appointment, Guid> appointmentRepository,
        ICurrentTenant currentTenant)
    {
        _notificationManager = notificationManager;
        _appointmentRepository = appointmentRepository;
        _currentTenant = currentTenant;
    }

    [UnitOfWork]
    public virtual async Task HandleEventAsync(AppointmentDocumentUploadedEto eventData)
    {
        if (eventData == null)
        {
            return;
        }
        using (_currentTenant.Change(eventData.TenantId))
        {
            var appointment = await _appointmentRepository.FindAsync(eventData.AppointmentId);
            var confirmation = appointment?.RequestConfirmationNumber ?? "an appointment";
            await _notificationManager.RaiseForOfficeStaffAsync(
                AppNotificationType.DocumentUploaded,
                title: "Document uploaded",
                body: $"A document was uploaded for {confirmation}.",
                url: InAppNotificationUrls.AppointmentDetail(eventData.AppointmentId));
        }
    }
}
