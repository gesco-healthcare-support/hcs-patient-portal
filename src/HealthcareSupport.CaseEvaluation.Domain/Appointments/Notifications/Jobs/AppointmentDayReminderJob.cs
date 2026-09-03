using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.MultiTenancy;
using HealthcareSupport.CaseEvaluation.Notifications;
using HealthcareSupport.CaseEvaluation.Settings;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Settings;
using Volo.Abp.Timing;
using HealthcareSupport.CaseEvaluation.Timing;
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
    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly ITenantWorkRunner _tenantWorkRunner;
    private readonly IAppointmentRecipientResolver _recipientResolver;
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly ISettingProvider _settingProvider;
    private readonly ILogger<AppointmentDayReminderJob> _logger;
    private readonly IClock _clock;

    public AppointmentDayReminderJob(
        IRepository<Appointment, Guid> appointmentRepository,
        ITenantWorkRunner tenantWorkRunner,
        IAppointmentRecipientResolver recipientResolver,
        IBackgroundJobManager backgroundJobManager,
        ISettingProvider settingProvider,
        ILogger<AppointmentDayReminderJob> logger,
        IClock clock)
    {
        _appointmentRepository = appointmentRepository;
        _tenantWorkRunner = tenantWorkRunner;
        _recipientResolver = recipientResolver;
        _backgroundJobManager = backgroundJobManager;
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
        await _tenantWorkRunner.ForEachOfficeAsync(async _ =>
        {
            officeCount++;
            enqueuedTotal += await ProcessTenantAsync();
        });
        _logger.LogInformation(
            "AppointmentDayReminderJob: enqueued {Total} reminder emails across {TenantCount} tenants.",
            enqueuedTotal,
            officeCount);
    }

    private async Task<int> ProcessTenantAsync()
    {
        if (!await _settingProvider.GetAsync<bool>(CaseEvaluationSettings.RemindersPolicy.RemindersEnabled))
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

        var enqueued = 0;
        foreach (var appointment in eligible)
        {
            var recipients = await _recipientResolver.ResolveAsync(
                appointment.Id,
                NotificationKind.AppointmentDayReminder);
            var daysUntil = (int)(appointment.AppointmentDate.Date - todayPacific).TotalDays;
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
