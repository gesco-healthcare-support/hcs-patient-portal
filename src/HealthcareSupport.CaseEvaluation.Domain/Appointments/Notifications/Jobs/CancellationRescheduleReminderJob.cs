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
    private static readonly AppointmentStatusType[] InScopeStatuses =
    {
        AppointmentStatusType.CancellationRequested,
        AppointmentStatusType.RescheduleRequested,
        AppointmentStatusType.CancelledLate,
    };

    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly ITenantWorkRunner _tenantWorkRunner;
    private readonly IAppointmentRecipientResolver _recipientResolver;
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly ISettingProvider _settingProvider;
    private readonly ILogger<CancellationRescheduleReminderJob> _logger;
    private readonly IClock _clock;

    public CancellationRescheduleReminderJob(
        IRepository<Appointment, Guid> appointmentRepository,
        ITenantWorkRunner tenantWorkRunner,
        IAppointmentRecipientResolver recipientResolver,
        IBackgroundJobManager backgroundJobManager,
        ISettingProvider settingProvider,
        ILogger<CancellationRescheduleReminderJob> logger,
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

    public const string RecurringJobId = "appt-cancellation-reschedule-reminder";
    public const string CronExpression = "0 8 * * *";

    [UnitOfWork]
    public virtual async Task ExecuteAsync()
    {
        _logger.LogInformation("CancellationRescheduleReminderJob: starting daily run.");
        var enqueuedTotal = 0;
        var officeCount = 0;
        await _tenantWorkRunner.ForEachOfficeAsync(async _ =>
        {
            officeCount++;
            enqueuedTotal += await ProcessTenantAsync();
        });
        _logger.LogInformation(
            "CancellationRescheduleReminderJob: enqueued {Total} reminder emails across {TenantCount} tenants.",
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
                CaseEvaluationSettings.RemindersPolicy.Sec34eElapsedDayAnchors));

        // 2026-08-31: BOTH sides of the elapsed-day subtraction were UTC dates. Correct only
        // because the cron fires at 08:00 Pacific, when UTC is 15:00 the same day; an evening run
        // or a moved cron silently shifted the elapsed-day count by one and fired a reminder a day
        // early. LastModificationTime is a UTC INSTANT, so its Pacific calendar date is what the
        // elapsed-day anchors are actually about.
        var todayPacific = PacificTime.TodayFrom(_clock.Now);
        var queryable = await _appointmentRepository.GetQueryableAsync();
        var eligible = queryable
            .Where(a => InScopeStatuses.Contains(a.AppointmentStatus))
            .ToList()
            .Where(a => a.LastModificationTime.HasValue &&
                        cadence.ShouldFire((int)(todayPacific - PacificTime.TodayFrom(a.LastModificationTime.Value)).TotalDays))
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
