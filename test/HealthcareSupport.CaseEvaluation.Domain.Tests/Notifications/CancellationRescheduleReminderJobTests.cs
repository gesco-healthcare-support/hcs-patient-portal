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
            NullLogger<CancellationRescheduleReminderJob>.Instance,
            ReminderJobTestHarness.Clock());

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
        // CreationTime / LastModificationTime are UTC INSTANTS, so the fixture dates them from the
        // harness's pinned instant, not from a Pacific wall-clock date. Dating them from
        // PacificToday would build a value whose Pacific date is a day earlier than intended --
        // the same confusion the production bug came from.
        var since = ReminderJobTestHarness.NowUtc;
        var (job, resolver) = Build(false, CancelRequestedModifiedOn(OnAnchorId, since.AddDays(-45)));

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
            CancelRequestedModifiedOn(OnAnchorId, since.AddDays(-45)),
            CancelRequestedModifiedOn(OffAnchorId, since.AddDays(-46)));

        await job.ExecuteAsync();

        await resolver.Received(1).ResolveAsync(OnAnchorId, NotificationKind.CancellationRescheduleReminder);
        await resolver.DidNotReceive().ResolveAsync(OffAnchorId, Arg.Any<NotificationKind>());
    }
}
