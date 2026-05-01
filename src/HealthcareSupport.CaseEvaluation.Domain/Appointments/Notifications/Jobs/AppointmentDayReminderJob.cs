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
/// W2-10: appointment-day reminder. Fires daily at 07:00 Pacific Time
/// (earlier than the two CCR jobs so T-1-day reminders go out before the
/// office opens). For each tenant, locates Approved appointments whose
/// AppointmentDate falls T-7 days OR T-1 day from today, resolves all
/// parties, enqueues per-recipient reminder emails.
///
/// Holiday-aware skip on T-1 (skip the reminder if T-1 is a US federal
/// or California state holiday) is post-MVP per the deferred ledger;
/// MVP fires unconditionally on the calendar day matches.
/// </summary>
public class AppointmentDayReminderJob : ITransientDependency
{
    private static readonly int[] ReminderTMinusDays = { 7, 1 };

    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly IDataFilter _dataFilter;
    private readonly ICurrentTenant _currentTenant;
    private readonly IAppointmentRecipientResolver _recipientResolver;
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly ILogger<AppointmentDayReminderJob> _logger;

    public AppointmentDayReminderJob(
        IRepository<Appointment, Guid> appointmentRepository,
        IDataFilter dataFilter,
        ICurrentTenant currentTenant,
        IAppointmentRecipientResolver recipientResolver,
        IBackgroundJobManager backgroundJobManager,
        ILogger<AppointmentDayReminderJob> logger)
    {
        _appointmentRepository = appointmentRepository;
        _dataFilter = dataFilter;
        _currentTenant = currentTenant;
        _recipientResolver = recipientResolver;
        _backgroundJobManager = backgroundJobManager;
        _logger = logger;
    }

    public const string RecurringJobId = "appt-day-reminder";
    public const string CronExpression = "0 7 * * *";

    [UnitOfWork]
    public virtual async Task ExecuteAsync()
    {
        _logger.LogInformation("AppointmentDayReminderJob: starting daily run.");
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
            "AppointmentDayReminderJob: enqueued {Total} reminder emails across {TenantCount} tenants.",
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
        var todayUtc = DateTime.UtcNow.Date;
        var queryable = await _appointmentRepository.GetQueryableAsync();
        var eligible = queryable
            .Where(a => a.AppointmentStatus == AppointmentStatusType.Approved)
            .ToList()
            .Where(a => ReminderTMinusDays.Any(d => a.AppointmentDate.Date == todayUtc.AddDays(d)))
            .ToList();

        var enqueued = 0;
        foreach (var appointment in eligible)
        {
            var recipients = await _recipientResolver.ResolveAsync(
                appointment.Id,
                NotificationKind.AppointmentDayReminder);
            var daysUntil = (int)(appointment.AppointmentDate.Date - todayUtc).TotalDays;
            var when = daysUntil == 1 ? "tomorrow" : $"in {daysUntil} days";
            var subject = $"Reminder: appointment {appointment.RequestConfirmationNumber} {when}";
            var body = $"<p>Appointment confirmation #{appointment.RequestConfirmationNumber} is scheduled for {appointment.AppointmentDate:MMM d, yyyy h:mm tt}.</p><p>Please make any arrangements needed and confirm attendance.</p>";
            foreach (var args in recipients)
            {
                args.Subject = subject;
                args.Body = body;
                args.IsBodyHtml = true;
                args.Context = $"Reminder/AppointmentDay/T-{daysUntil}/{args.Role}/{appointment.Id}";
                await _backgroundJobManager.EnqueueAsync(args);
                enqueued++;
            }
        }
        return enqueued;
    }
}
