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
/// Group L wiring tests for <see cref="RequestSchedulingReminderJob"/> (CCR
/// Sec. 31.5): the RemindersEnabled gate mutes the job, and the elapsed-day
/// anchor setting (days since the request was created) drives which Pending
/// requests fire.
/// </summary>
public class RequestSchedulingReminderJobTests
{
    private static readonly Guid OnAnchorId = Guid.Parse("bbbb0001-0000-0000-0000-000000000001");
    private static readonly Guid OffAnchorId = Guid.Parse("bbbb0002-0000-0000-0000-000000000002");

    private static (RequestSchedulingReminderJob Job, IAppointmentRecipientResolver Resolver) Build(
        bool enabled, params Appointment[] appointments)
    {
        var resolver = Substitute.For<IAppointmentRecipientResolver>();
        resolver.ResolveAsync(Arg.Any<Guid>(), Arg.Any<NotificationKind>())
            .Returns(new List<SendAppointmentEmailArgs>());

        var job = new RequestSchedulingReminderJob(
            ReminderJobTestHarness.AppointmentRepo(appointments),
            ReminderJobTestHarness.NoopDataFilter(),
            ReminderJobTestHarness.NoopCurrentTenant(),
            resolver,
            Substitute.For<IBackgroundJobManager>(),
            ReminderJobTestHarness.Settings(
                enabled,
                CaseEvaluationSettings.RemindersPolicy.Sec31_5ElapsedDayAnchors,
                "30,60,75,85,90"),
            NullLogger<RequestSchedulingReminderJob>.Instance);

        return (job, resolver);
    }

    private static Appointment PendingCreatedOn(Guid id, DateTime createdOn)
    {
        var appt = ReminderJobTestHarness.Appt(id, AppointmentStatusType.Pending, createdOn);
        appt.CreationTime = createdOn;
        return appt;
    }

    [Fact]
    public async Task Muted_when_reminders_disabled()
    {
        var today = DateTime.UtcNow.Date;
        var (job, resolver) = Build(false, PendingCreatedOn(OnAnchorId, today.AddDays(-30)));

        await job.ExecuteAsync();

        await resolver.DidNotReceive().ResolveAsync(Arg.Any<Guid>(), Arg.Any<NotificationKind>());
    }

    [Fact]
    public async Task Fires_only_for_elapsed_day_anchors_when_enabled()
    {
        var today = DateTime.UtcNow.Date;
        var (job, resolver) = Build(
            true,
            PendingCreatedOn(OnAnchorId, today.AddDays(-30)),
            PendingCreatedOn(OffAnchorId, today.AddDays(-31)));

        await job.ExecuteAsync();

        await resolver.Received(1).ResolveAsync(OnAnchorId, NotificationKind.RequestSchedulingReminder);
        await resolver.DidNotReceive().ResolveAsync(OffAnchorId, Arg.Any<NotificationKind>());
    }
}
