using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.MultiTenancy;
using HealthcareSupport.CaseEvaluation.Notifications;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using HealthcareSupport.CaseEvaluation.Settings;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Local;
using Volo.Abp.Settings;
using Volo.Abp.Timing;
using HealthcareSupport.CaseEvaluation.Timing;
using Volo.Abp.Uow;

namespace HealthcareSupport.CaseEvaluation.Appointments.Notifications.Jobs;

/// <summary>
/// W2-10: appointment-day reminder. Fires daily at 07:00 Pacific Time
/// (earlier than the two CCR jobs so T-1-day reminders go out before the
/// office opens). For each tenant, locates Approved appointments whose
/// AppointmentDate falls T-7 days OR T-1 day from today and publishes one
/// <see cref="AppointmentDayReminderEto"/> per appointment. Since 2026-11-03 the
/// email itself is sent by <c>AppointmentDayReminderEmailHandler</c> through the
/// notification dispatcher and the editable <c>AppointmentDayReminder</c> template
/// (to the patient, the office inbox and the Intake Staff); the job used to build
/// inline text and enqueue it straight to the mail job, bypassing the outbox.
///
/// <para>Gated by its OWN setting, <see cref="CaseEvaluationSettings.RemindersPolicy.AppointmentDayReminderEnabled"/>
/// (default on), not the global <c>RemindersEnabled</c> (default off) the other three
/// reminder jobs still honor: the office wants only this reminder switched on.</para>
///
/// Holiday-aware skip on T-1 (skip the reminder if T-1 is a US federal
/// or California state holiday) is post-MVP per the deferred ledger;
/// MVP fires unconditionally on the calendar day matches.
/// </summary>
public class AppointmentDayReminderJob : ITransientDependency
{
    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly ITenantWorkRunner _tenantWorkRunner;
    private readonly ILocalEventBus _localEventBus;
    private readonly ISettingProvider _settingProvider;
    private readonly ILogger<AppointmentDayReminderJob> _logger;
    private readonly IClock _clock;

    public AppointmentDayReminderJob(
        IRepository<Appointment, Guid> appointmentRepository,
        ITenantWorkRunner tenantWorkRunner,
        ILocalEventBus localEventBus,
        ISettingProvider settingProvider,
        ILogger<AppointmentDayReminderJob> logger,
        IClock clock)
    {
        _appointmentRepository = appointmentRepository;
        _tenantWorkRunner = tenantWorkRunner;
        _localEventBus = localEventBus;
        _settingProvider = settingProvider;
        _logger = logger;
        _clock = clock;
    }

    public const string RecurringJobId = "appt-day-reminder";
    public const string CronExpression = "0 7 * * *";

    [UnitOfWork]
    public virtual async Task ExecuteAsync()
    {
        _logger.LogInformation("AppointmentDayReminderJob: starting daily run.");
        var enqueuedTotal = 0;
        var officeCount = 0;
        await _tenantWorkRunner.ForEachOfficeAsync(async officeId =>
        {
            officeCount++;
            enqueuedTotal += await ProcessTenantAsync(officeId);
        });
        _logger.LogInformation(
            "AppointmentDayReminderJob: raised {Total} reminder events across {TenantCount} tenants.",
            enqueuedTotal,
            officeCount);
    }

    private async Task<int> ProcessTenantAsync(Guid? tenantId)
    {
        if (!await _settingProvider.GetAsync<bool>(CaseEvaluationSettings.RemindersPolicy.AppointmentDayReminderEnabled))
        {
            return 0;
        }

        var cadence = new ReminderCadence(
            await _settingProvider.GetOrNullAsync(
                CaseEvaluationSettings.RemindersPolicy.AppointmentDayTMinusAnchors));

        // 2026-08-31: PACIFIC today. This was DateTime.UtcNow.Date, which is correct ONLY because
        // the cron fires at 07:00 Pacific, when UTC is 14:00 the same day. Move the cron past 5pm
        // Pacific -- or trigger this job manually in the evening -- and it silently used tomorrow,
        // sending day-of reminders a day early. AppointmentDate is a calendar date, so both sides
        // of the subtraction below are now Pacific wall-clock dates.
        var todayPacific = PacificTime.TodayFrom(_clock.Now);
        var queryable = await _appointmentRepository.GetQueryableAsync();
        var eligible = queryable
            .Where(a => a.AppointmentStatus == AppointmentStatusType.Approved)
            .ToList()
            .Where(a => cadence.ShouldFire((int)(a.AppointmentDate.Date - todayPacific).TotalDays))
            .ToList();

        var raised = 0;
        foreach (var appointment in eligible)
        {
            await _localEventBus.PublishAsync(new AppointmentDayReminderEto
            {
                AppointmentId = appointment.Id,
                TenantId = tenantId,
                DaysUntil = (int)(appointment.AppointmentDate.Date - todayPacific).TotalDays,
                OccurredAt = _clock.Now.ToUniversalTime(),
            });
            raised++;
        }
        return raised;
    }
}
