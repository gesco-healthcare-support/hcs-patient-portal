using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using Microsoft.Extensions.Logging;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Local;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace HealthcareSupport.CaseEvaluation.Notifications.Jobs;

/// <summary>
/// Phase 7 (Category 7, 2026-05-10) -- per-stakeholder due-date
/// approaching reminder. Mirrors OLD <c>SchedulerDomain.cs</c>:152.
/// Fires at 14 / 7 / 3 days before <c>Appointment.DueDate</c> for any
/// appointment not yet in a terminal status.
///
/// <para>Cron: 08:15 PT daily. Window choice (14/7/3) per Adrian
/// Decision (2026-05-10) -- OLD's stored proc decided the window
/// host-side and the rule was not in source; three cadences mirror the
/// likely intent and align with Reminder #5's pattern.</para>
///
/// <para>Subscribers: <c>DueDateApproachingEmailHandler</c> fans out to
/// every stakeholder via <c>IAppointmentRecipientResolver</c> and
/// dispatches the <c>AppointmentDueDateReminder</c> template.</para>
/// </summary>
public class DueDateApproachingJob : ITransientDependency
{
    public const string RecurringJobId = "appt-duedate-approaching";
    public const string CronExpression = "15 8 * * *";

    private static readonly int[] ReminderDaysBeforeDueDate = { 14, 7, 3 };

    // Exclude terminal / dead-end statuses so a cancelled or completed
    // appointment that happens to land in a 14/7/3-day window doesn't
    // fire spurious reminders. Status guard mirrors OLD's "active
    // appointments only" intent from the stored proc.
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
    private readonly ILogger<DueDateApproachingJob> _logger;

    public DueDateApproachingJob(
        IRepository<Appointment, Guid> appointmentRepository,
        IDataFilter dataFilter,
        ICurrentTenant currentTenant,
        ILocalEventBus localEventBus,
        ILogger<DueDateApproachingJob> logger)
    {
        _appointmentRepository = appointmentRepository;
        _dataFilter = dataFilter;
        _currentTenant = currentTenant;
        _localEventBus = localEventBus;
        _logger = logger;
    }

    [UnitOfWork]
    public virtual async Task ExecuteAsync()
    {
        _logger.LogInformation("DueDateApproachingJob: starting daily run.");
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
            if (!ReminderDaysBeforeDueDate.Contains(daysUntil))
            {
                continue;
            }

            await _localEventBus.PublishAsync(new DueDateApproachingEto
            {
                AppointmentId = candidate.Id,
                TenantId = tenantId,
                DaysUntilDue = daysUntil,
                OccurredAt = nowUtc,
            });
            published++;
        }

        _logger.LogInformation(
            "DueDateApproachingJob: tenant {TenantId} fired {Count} due-date reminder event(s).",
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
