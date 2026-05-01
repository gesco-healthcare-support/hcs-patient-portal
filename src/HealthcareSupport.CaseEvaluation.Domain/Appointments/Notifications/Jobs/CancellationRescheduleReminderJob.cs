using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Enums;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace HealthcareSupport.CaseEvaluation.Appointments.Notifications.Jobs;

/// <summary>
/// W2-10: CCR Title 8 Sec. 34(e) -- cancellation/reschedule clock reminder.
/// Fires daily at 08:00 Pacific Time. For each tenant, locates appointments
/// in Cancel/Reschedule states (CancellationRequested, RescheduleRequested,
/// CancelledLate) where the 60-day reschedule clock has been running for
/// 45 or 55 days (default windows; admin-tunable post-MVP), resolves all
/// parties, enqueues per-recipient reminder emails.
///
/// MVP scope: simple elapsed-day check from RequestConfirmationNumber
/// creation; the full Sec. 34(e) clock semantics (start = cancellation
/// request submitted by attorney, etc.) lands when the W3
/// appointment-change-requests cap ships and we have the cancellation
/// request entity to anchor the clock to.
/// </summary>
public class CancellationRescheduleReminderJob : ITransientDependency
{
    private static readonly int[] ReminderElapsedDays = { 45, 55 };

    private static readonly AppointmentStatusType[] InScopeStatuses =
    {
        AppointmentStatusType.CancellationRequested,
        AppointmentStatusType.RescheduleRequested,
        AppointmentStatusType.CancelledLate,
    };

    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly IDataFilter _dataFilter;
    private readonly ICurrentTenant _currentTenant;
    private readonly IAppointmentRecipientResolver _recipientResolver;
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly ILogger<CancellationRescheduleReminderJob> _logger;

    public CancellationRescheduleReminderJob(
        IRepository<Appointment, Guid> appointmentRepository,
        IDataFilter dataFilter,
        ICurrentTenant currentTenant,
        IAppointmentRecipientResolver recipientResolver,
        IBackgroundJobManager backgroundJobManager,
        ILogger<CancellationRescheduleReminderJob> logger)
    {
        _appointmentRepository = appointmentRepository;
        _dataFilter = dataFilter;
        _currentTenant = currentTenant;
        _recipientResolver = recipientResolver;
        _backgroundJobManager = backgroundJobManager;
        _logger = logger;
    }

    public const string RecurringJobId = "appt-cancellation-reschedule-reminder";
    public const string CronExpression = "0 8 * * *";

    [UnitOfWork]
    public virtual async Task ExecuteAsync()
    {
        _logger.LogInformation("CancellationRescheduleReminderJob: starting daily run.");
        var tenantIds = await GetDistinctTenantIdsAsync();
        var enqueuedTotal = 0;
        foreach (var tenantId in tenantIds)
        {
            using (_currentTenant.Change(tenantId))
            {
                enqueuedTotal += await ProcessTenantAsync();
            }
        }
        _logger.LogInformation(
            "CancellationRescheduleReminderJob: enqueued {Total} reminder emails across {TenantCount} tenants.",
            enqueuedTotal,
            tenantIds.Count);
    }

    private async Task<System.Collections.Generic.List<Guid?>> GetDistinctTenantIdsAsync()
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var queryable = await _appointmentRepository.GetQueryableAsync();
            return queryable.Select(a => a.TenantId).Distinct().ToList();
        }
    }

    private async Task<int> ProcessTenantAsync()
    {
        var nowUtc = DateTime.UtcNow.Date;
        var queryable = await _appointmentRepository.GetQueryableAsync();
        var eligible = queryable
            .Where(a => InScopeStatuses.Contains(a.AppointmentStatus))
            .ToList()
            .Where(a => ReminderElapsedDays.Any(d => a.LastModificationTime?.Date == nowUtc.AddDays(-d)))
            .ToList();

        var enqueued = 0;
        foreach (var appointment in eligible)
        {
            var recipients = await _recipientResolver.ResolveAsync(
                appointment.Id,
                NotificationKind.CancellationRescheduleReminder);
            var subject = $"Reminder: cancellation/reschedule clock running for {appointment.RequestConfirmationNumber}";
            var body = $"<p>Per CCR Title 8 Sec. 34(e), the 60-day reschedule clock for confirmation #{appointment.RequestConfirmationNumber} continues. Please complete the action or extend the request.</p>";
            foreach (var args in recipients)
            {
                args.Subject = subject;
                args.Body = body;
                args.IsBodyHtml = true;
                args.Context = $"Reminder/Sec34e/{args.Role}/{appointment.Id}";
                await _backgroundJobManager.EnqueueAsync(args);
                enqueued++;
            }
        }
        return enqueued;
    }
}
