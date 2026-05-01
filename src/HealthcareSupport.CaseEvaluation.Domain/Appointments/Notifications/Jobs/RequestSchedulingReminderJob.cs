using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments.Jobs;
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
/// W2-10: CCR Title 8 Sec. 31.5 -- request-scheduling reminder. Fires daily
/// at 08:00 Pacific Time. For each tenant, locates Pending or AwaitingMoreInfo
/// appointments whose request submission has been outstanding for the
/// elapsed-day windows defined in CCR (default 30 / 60 / 75 / 85 / 90 days
/// since RequestConfirmationNumber assignment), resolves all parties via
/// <see cref="IAppointmentRecipientResolver"/>, and enqueues per-recipient
/// <c>SendAppointmentEmailJob</c> instances.
///
/// Registered as a Hangfire RecurringJob in
/// <c>CaseEvaluationHttpApiHostModule.ConfigureHangfireRecurringJobs</c>.
/// </summary>
public class RequestSchedulingReminderJob : ITransientDependency
{
    private static readonly int[] ReminderElapsedDays = { 30, 60, 75, 85, 90 };

    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly IDataFilter _dataFilter;
    private readonly ICurrentTenant _currentTenant;
    private readonly IAppointmentRecipientResolver _recipientResolver;
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly ILogger<RequestSchedulingReminderJob> _logger;

    public RequestSchedulingReminderJob(
        IRepository<Appointment, Guid> appointmentRepository,
        IDataFilter dataFilter,
        ICurrentTenant currentTenant,
        IAppointmentRecipientResolver recipientResolver,
        IBackgroundJobManager backgroundJobManager,
        ILogger<RequestSchedulingReminderJob> logger)
    {
        _appointmentRepository = appointmentRepository;
        _dataFilter = dataFilter;
        _currentTenant = currentTenant;
        _recipientResolver = recipientResolver;
        _backgroundJobManager = backgroundJobManager;
        _logger = logger;
    }

    public const string RecurringJobId = "appt-request-scheduling-reminder";
    public const string CronExpression = "0 8 * * *";

    [UnitOfWork]
    public virtual async Task ExecuteAsync()
    {
        _logger.LogInformation("RequestSchedulingReminderJob: starting daily run.");
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
            "RequestSchedulingReminderJob: enqueued {Total} reminder emails across {TenantCount} tenants.",
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
        // Match the request-creation date against each elapsed-day window;
        // ABP CreationTime is set at appointment row insert (the request submit
        // moment), so windowStart = today - daysElapsed (date-only).
        var eligible = queryable
            .Where(a =>
                (a.AppointmentStatus == AppointmentStatusType.Pending ||
                 a.AppointmentStatus == AppointmentStatusType.AwaitingMoreInfo))
            .ToList()
            .Where(a => ReminderElapsedDays.Any(d => a.CreationTime.Date == nowUtc.AddDays(-d)))
            .ToList();

        var enqueued = 0;
        foreach (var appointment in eligible)
        {
            var recipients = await _recipientResolver.ResolveAsync(
                appointment.Id,
                NotificationKind.RequestSchedulingReminder);
            var subject = $"Reminder: appointment request {appointment.RequestConfirmationNumber} still pending";
            var body = $"<p>Confirmation #{appointment.RequestConfirmationNumber} has not been scheduled yet.</p><p>Per CCR Title 8 Sec. 31.5 reminder schedule, please review and respond.</p>";
            foreach (var args in recipients)
            {
                args.Subject = subject;
                args.Body = body;
                args.IsBodyHtml = true;
                args.Context = $"Reminder/Sec31.5/{args.Role}/{appointment.Id}";
                await _backgroundJobManager.EnqueueAsync(args);
                enqueued++;
            }
        }
        return enqueued;
    }
}
