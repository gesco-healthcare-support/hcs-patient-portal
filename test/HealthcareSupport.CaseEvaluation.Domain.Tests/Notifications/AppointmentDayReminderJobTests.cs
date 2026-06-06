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
/// Group L wiring tests for <see cref="AppointmentDayReminderJob"/>: the
/// RemindersEnabled gate mutes the job, and the day-anchor setting (T-minus
/// days until the appointment) drives which appointments fire. Cadence parsing
/// itself is covered by <see cref="ReminderCadenceTests"/>.
/// </summary>
public class AppointmentDayReminderJobTests
{
    private static readonly Guid OnAnchorId = Guid.Parse("aaaa0001-0000-0000-0000-000000000001");
    private static readonly Guid OffAnchorId = Guid.Parse("aaaa0002-0000-0000-0000-000000000002");

    private static (AppointmentDayReminderJob Job, IAppointmentRecipientResolver Resolver) Build(
        bool enabled, params Appointment[] appointments)
    {
        var resolver = Substitute.For<IAppointmentRecipientResolver>();
        resolver.ResolveAsync(Arg.Any<Guid>(), Arg.Any<NotificationKind>())
            .Returns(new List<SendAppointmentEmailArgs>());

        var job = new AppointmentDayReminderJob(
            ReminderJobTestHarness.AppointmentRepo(appointments),
            ReminderJobTestHarness.NoopDataFilter(),
            ReminderJobTestHarness.NoopCurrentTenant(),
            resolver,
            Substitute.For<IBackgroundJobManager>(),
            ReminderJobTestHarness.Settings(
                enabled,
                CaseEvaluationSettings.RemindersPolicy.AppointmentDayTMinusAnchors,
                "7,1"),
            NullLogger<AppointmentDayReminderJob>.Instance);

        return (job, resolver);
    }

    [Fact]
    public async Task Muted_when_reminders_disabled()
    {
        var today = DateTime.UtcNow.Date;
        var (job, resolver) = Build(
            enabled: false,
            ReminderJobTestHarness.Appt(OnAnchorId, AppointmentStatusType.Approved, today.AddDays(7)));

        await job.ExecuteAsync();

        await resolver.DidNotReceive().ResolveAsync(Arg.Any<Guid>(), Arg.Any<NotificationKind>());
    }

    [Fact]
    public async Task Fires_only_for_anchor_days_when_enabled()
    {
        var today = DateTime.UtcNow.Date;
        var (job, resolver) = Build(
            enabled: true,
            ReminderJobTestHarness.Appt(OnAnchorId, AppointmentStatusType.Approved, today.AddDays(7)),
            ReminderJobTestHarness.Appt(OffAnchorId, AppointmentStatusType.Approved, today.AddDays(5)));

        await job.ExecuteAsync();

        await resolver.Received(1).ResolveAsync(OnAnchorId, NotificationKind.AppointmentDayReminder);
        await resolver.DidNotReceive().ResolveAsync(OffAnchorId, Arg.Any<NotificationKind>());
    }
}
