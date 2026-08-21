using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications.Jobs;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Volo.Abp.BackgroundJobs;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Notifications.Jobs;

/// <summary>
/// Group L wiring tests for <see cref="CancellationRescheduleReminderJob"/>
/// (CCR Sec. 34(e)): the RemindersEnabled gate mutes the job, and the
/// elapsed-day anchor setting (days since last modification) drives which
/// cancel/reschedule-clock appointments fire.
/// </summary>
public class CancellationRescheduleReminderJobTests
{
    private static readonly Guid OnAnchorId = Guid.Parse("cccc0001-0000-0000-0000-000000000001");
    private static readonly Guid OffAnchorId = Guid.Parse("cccc0002-0000-0000-0000-000000000002");

    private static (CancellationRescheduleReminderJob Job, IAppointmentRecipientResolver Resolver) Build(
        bool enabled, params Appointment[] appointments)
    {
        var resolver = Substitute.For<IAppointmentRecipientResolver>();
        resolver.ResolveAsync(Arg.Any<Guid>(), Arg.Any<NotificationKind>())
            .Returns(new List<SendAppointmentEmailArgs>());

        var job = new CancellationRescheduleReminderJob(
            ReminderJobTestHarness.AppointmentRepo(appointments),
            ReminderJobTestHarness.TenantWorkRunner(),
            resolver,
            Substitute.For<IBackgroundJobManager>(),
            ReminderJobTestHarness.Settings(
                enabled,
                CaseEvaluationSettings.RemindersPolicy.Sec34eElapsedDayAnchors,
                "45,55"),
            NullLogger<CancellationRescheduleReminderJob>.Instance);

        return (job, resolver);
    }

    private static Appointment CancelRequestedModifiedOn(Guid id, DateTime modifiedOn)
    {
        var appt = ReminderJobTestHarness.Appt(id, AppointmentStatusType.CancellationRequested, modifiedOn);
        appt.LastModificationTime = modifiedOn;
        return appt;
    }

    [Fact]
    public async Task Muted_when_reminders_disabled()
    {
        var today = DateTime.UtcNow.Date;
        var (job, resolver) = Build(false, CancelRequestedModifiedOn(OnAnchorId, today.AddDays(-45)));

        await job.ExecuteAsync();

        await resolver.DidNotReceive().ResolveAsync(Arg.Any<Guid>(), Arg.Any<NotificationKind>());
    }

    [Fact]
    public async Task Fires_only_for_elapsed_day_anchors_when_enabled()
    {
        var today = DateTime.UtcNow.Date;
        var (job, resolver) = Build(
            true,
            CancelRequestedModifiedOn(OnAnchorId, today.AddDays(-45)),
            CancelRequestedModifiedOn(OffAnchorId, today.AddDays(-46)));

        await job.ExecuteAsync();

        await resolver.Received(1).ResolveAsync(OnAnchorId, NotificationKind.CancellationRescheduleReminder);
        await resolver.DidNotReceive().ResolveAsync(OffAnchorId, Arg.Any<NotificationKind>());
    }
}
