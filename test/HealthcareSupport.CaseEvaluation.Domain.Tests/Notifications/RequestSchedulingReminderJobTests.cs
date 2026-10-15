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
            ReminderJobTestHarness.TenantWorkRunner(),
            resolver,
            Substitute.For<IBackgroundJobManager>(),
            ReminderJobTestHarness.Settings(
                enabled,
                CaseEvaluationSettings.RemindersPolicy.Sec31_5ElapsedDayAnchors,
                "30,60,75,85,90"),
            NullLogger<RequestSchedulingReminderJob>.Instance,
            ReminderJobTestHarness.Clock());

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
        // CreationTime / LastModificationTime are UTC INSTANTS, so the fixture dates them from the
        // harness's pinned instant, not from a Pacific wall-clock date. Dating them from
        // PacificToday would build a value whose Pacific date is a day earlier than intended --
        // the same confusion the production bug came from.
        var since = ReminderJobTestHarness.NowUtc;
        var (job, resolver) = Build(false, PendingCreatedOn(OnAnchorId, since.AddDays(-30)));

        await job.ExecuteAsync();

        await resolver.DidNotReceive().ResolveAsync(Arg.Any<Guid>(), Arg.Any<NotificationKind>());
    }

    [Fact]
    public async Task Fires_only_for_elapsed_day_anchors_when_enabled()
    {
        // CreationTime / LastModificationTime are UTC INSTANTS, so the fixture dates them from the
        // harness's pinned instant, not from a Pacific wall-clock date. Dating them from
        // PacificToday would build a value whose Pacific date is a day earlier than intended --
        // the same confusion the production bug came from.
        var since = ReminderJobTestHarness.NowUtc;
        var (job, resolver) = Build(
            true,
            PendingCreatedOn(OnAnchorId, since.AddDays(-30)),
            PendingCreatedOn(OffAnchorId, since.AddDays(-31)));

        await job.ExecuteAsync();

        await resolver.Received(1).ResolveAsync(OnAnchorId, NotificationKind.RequestSchedulingReminder);
        await resolver.DidNotReceive().ResolveAsync(OffAnchorId, Arg.Any<NotificationKind>());
    }
}
