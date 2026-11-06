using HealthcareSupport.CaseEvaluation.EmailStops;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications.Jobs;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using HealthcareSupport.CaseEvaluation.Settings;
using HealthcareSupport.CaseEvaluation.Timing;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.EventBus.Local;
using Volo.Abp.Settings;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Notifications.Jobs;

/// <summary>
/// The office turned ON only the appointment reminder (7 and 1 days before). The other three reminder jobs
/// -- the due-date reminder, the pending-request reminder and the cancellation/reschedule follow-up -- must
/// stay OFF: each is gated by the global <c>RemindersEnabled</c> setting (default off), and the new
/// appointment-day switch must not leak into them. Every Fact keeps the appointment-day switch ON and an
/// on-anchor appointment in the repository, and flips only the global switch: ON the job acts (the
/// positive control), OFF it does nothing.
/// </summary>
public class AppointmentDayReminderSwitchIsolationTests
{
    private static ISettingProvider Settings(bool globalOn)
    {
        var settings = Substitute.For<ISettingProvider>();
        settings.GetOrNullAsync(CaseEvaluationSettings.RemindersPolicy.AppointmentDayReminderEnabled).Returns("true");
        settings.GetOrNullAsync(CaseEvaluationSettings.RemindersPolicy.RemindersEnabled).Returns(globalOn ? "true" : "false");
        settings.GetOrNullAsync(CaseEvaluationSettings.RemindersPolicy.Sec34eElapsedDayAnchors).Returns("45,55");
        settings.GetOrNullAsync(CaseEvaluationSettings.RemindersPolicy.Sec31_5ElapsedDayAnchors).Returns("30,60");
        settings.GetOrNullAsync(CaseEvaluationSettings.RemindersPolicy.DueDateApproachingAnchors).Returns("14,7,3");
        return settings;
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task The_cancellation_reschedule_reminder_follows_only_the_global_switch(bool globalOn, bool acts)
    {
        var since = ReminderJobTestHarness.NowUtc;
        var appt = ReminderJobTestHarness.Appt(Guid.NewGuid(), AppointmentStatusType.CancellationRequested, since.AddDays(-45));
        appt.LastModificationTime = since.AddDays(-45);
        var resolver = Substitute.For<IAppointmentRecipientResolver>();
        resolver.ResolveAsync(Arg.Any<Guid>(), Arg.Any<NotificationKind>()).Returns(new List<SendAppointmentEmailArgs>());
        var job = new CancellationRescheduleReminderJob(
            ReminderJobTestHarness.AppointmentRepo(appt),
            ReminderJobTestHarness.TenantWorkRunner(),
            resolver,
            Substitute.For<IBackgroundJobManager>(),
            Settings(globalOn),
            NullLogger<CancellationRescheduleReminderJob>.Instance,
            ReminderJobTestHarness.Clock(), Substitute.For<IEmailStopPolicy>());

        await job.ExecuteAsync();

        resolver.ReceivedCalls().Any().ShouldBe(acts);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task The_pending_request_reminder_follows_only_the_global_switch(bool globalOn, bool acts)
    {
        var since = ReminderJobTestHarness.NowUtc;
        var appt = ReminderJobTestHarness.Appt(Guid.NewGuid(), AppointmentStatusType.Pending, since);
        appt.CreationTime = since.AddDays(-30);
        var resolver = Substitute.For<IAppointmentRecipientResolver>();
        resolver.ResolveAsync(Arg.Any<Guid>(), Arg.Any<NotificationKind>()).Returns(new List<SendAppointmentEmailArgs>());
        var job = new RequestSchedulingReminderJob(
            ReminderJobTestHarness.AppointmentRepo(appt),
            ReminderJobTestHarness.TenantWorkRunner(),
            resolver,
            Substitute.For<IBackgroundJobManager>(),
            Settings(globalOn),
            NullLogger<RequestSchedulingReminderJob>.Instance,
            ReminderJobTestHarness.Clock(), Substitute.For<IEmailStopPolicy>());

        await job.ExecuteAsync();

        resolver.ReceivedCalls().Any().ShouldBe(acts);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task The_due_date_reminder_follows_only_the_global_switch(bool globalOn, bool acts)
    {
        // This job reads the real clock (it takes no IClock), so the due date is dated from it.
        var today = PacificTime.TodayFrom(DateTime.UtcNow);
        var appt = new Appointment(
            id: Guid.NewGuid(),
            patientId: Guid.NewGuid(),
            identityUserId: null,
            appointmentTypeId: Guid.NewGuid(),
            locationId: Guid.NewGuid(),
            doctorAvailabilityId: Guid.NewGuid(),
            appointmentDate: today.AddDays(30),
            requestConfirmationNumber: "TEST-DUE001",
            appointmentStatus: AppointmentStatusType.Approved,
            dueDate: today.AddDays(7));
        var bus = Substitute.For<ILocalEventBus>();
        var job = new AppointmentReminderJob(
            ReminderJobTestHarness.AppointmentRepo(appt),
            ReminderJobTestHarness.TenantWorkRunner(),
            bus,
            Settings(globalOn),
            NullLogger<AppointmentReminderJob>.Instance);

        await job.ExecuteAsync();

        bus.ReceivedCalls().SelectMany(c => c.GetArguments()).OfType<AppointmentReminderEto>().Any().ShouldBe(acts);
    }
}
