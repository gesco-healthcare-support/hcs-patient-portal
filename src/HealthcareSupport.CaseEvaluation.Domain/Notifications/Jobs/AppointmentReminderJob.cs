using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using HealthcareSupport.CaseEvaluation.Settings;
using Microsoft.Extensions.Logging;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Local;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Settings;
using Volo.Abp.Uow;

namespace HealthcareSupport.CaseEvaluation.Notifications.Jobs;

/// <summary>
/// Group F (2026-06-09) -- single consolidated appointment-reminder job.
/// Replaces the three overlapping reminder jobs (DueDateApproaching,
/// DueDateDocumentIncomplete, PackageDocumentReminder). Fires once per active
/// appointment whose <c>DueDate</c> lands on a configured anchor (default
/// 14 / 7 / 3 days before due, from <c>RemindersPolicy.DueDateApproachingAnchors</c>)
/// and publishes one <see cref="AppointmentReminderEto"/>. The
/// <c>AppointmentReminderEmailHandler</c> then assembles the due-date nudge plus
/// any outstanding documents into ONE email To the booker (parties + office CC'd).
///
/// <para>Cron: 08:15 PT daily. <see cref="RecurringJobId"/> is kept at the
/// legacy "appt-duedate-approaching" string so renaming the class does NOT
/// orphan the existing Hangfire recurring entry; the two retired jobs'
/// recurring entries are purged via <c>RecurringJob.RemoveIfExists</c> in the
/// host module.</para>
/// </summary>
public class AppointmentReminderJob : ITransientDependency
{
    public const string RecurringJobId = "appt-duedate-approaching";
    public const string CronExpression = "15 8 * * *";

    // Exclude terminal / dead-end statuses so a cancelled or completed
    // appointment that happens to land on an anchor day does not fire a
    // spurious reminder.
    private static readonly AppointmentStatusType[] EligibleStatuses =
    {
        AppointmentStatusType.Pending,
        AppointmentStatusType.Approved,
        AppointmentStatusType.RescheduleRequested,
    };

    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly IDataFilter _dataFilter;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILocalEventBus _localEventBus;
    private readonly ISettingProvider _settingProvider;
    private readonly ILogger<AppointmentReminderJob> _logger;

    public AppointmentReminderJob(
        IRepository<Appointment, Guid> appointmentRepository,
        IDataFilter dataFilter,
        ICurrentTenant currentTenant,
        ILocalEventBus localEventBus,
        ISettingProvider settingProvider,
        ILogger<AppointmentReminderJob> logger)
    {
        _appointmentRepository = appointmentRepository;
        _dataFilter = dataFilter;
        _currentTenant = currentTenant;
        _localEventBus = localEventBus;
        _settingProvider = settingProvider;
        _logger = logger;
    }

    [UnitOfWork]
    public virtual async Task ExecuteAsync()
    {
        _logger.LogInformation("AppointmentReminderJob: starting daily run.");
        var nowUtc = DateTime.UtcNow;
        var todayDate = nowUtc.Date;
        var tenantIds = await GetDistinctTenantIdsAsync();

        foreach (var tenantId in tenantIds)
        {
            if (!tenantId.HasValue)
            {
                continue;
            }
            using (_currentTenant.Change(tenantId))
            {
                await ProcessTenantAsync(tenantId, todayDate, nowUtc);
            }
        }
    }

    private async Task ProcessTenantAsync(Guid? tenantId, DateTime todayDate, DateTime nowUtc)
    {
        if (!await _settingProvider.GetAsync<bool>(CaseEvaluationSettings.RemindersPolicy.RemindersEnabled))
        {
            return;
        }

        var cadence = new ReminderCadence(
            await _settingProvider.GetOrNullAsync(
                CaseEvaluationSettings.RemindersPolicy.DueDateApproachingAnchors));

        var queryable = await _appointmentRepository.GetQueryableAsync();
        var candidates = queryable
            .Where(a => a.DueDate.HasValue && EligibleStatuses.Contains(a.AppointmentStatus))
            .Select(a => new { a.Id, a.DueDate })
            .ToList();

        var published = 0;
        foreach (var candidate in candidates)
        {
            var dueDate = candidate.DueDate!.Value.Date;
            var daysUntil = (int)(dueDate - todayDate).TotalDays;
            if (!cadence.ShouldFire(daysUntil))
            {
                continue;
            }

            await _localEventBus.PublishAsync(new AppointmentReminderEto
            {
                AppointmentId = candidate.Id,
                TenantId = tenantId,
                DaysUntilDue = daysUntil,
                OccurredAt = nowUtc,
            });
            published++;
        }

        _logger.LogInformation(
            "AppointmentReminderJob: tenant {TenantId} fired {Count} reminder event(s).",
            tenantId,
            published);
    }

    private async Task<Guid?[]> GetDistinctTenantIdsAsync()
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var queryable = await _appointmentRepository.GetQueryableAsync();
            return queryable
                .Select(a => a.TenantId)
                .Distinct()
                .ToArray();
        }
    }
}
