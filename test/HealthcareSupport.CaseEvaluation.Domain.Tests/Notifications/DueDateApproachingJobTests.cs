using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using HealthcareSupport.CaseEvaluation.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Volo.Abp.EventBus.Local;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Notifications.Jobs;

/// <summary>
/// Group L wiring tests for <see cref="DueDateApproachingJob"/>: the
/// RemindersEnabled gate mutes the job, and the T-minus anchor setting (days
/// until the due date) drives which appointments fire.
/// </summary>
public class DueDateApproachingJobTests
{
    private static readonly Guid OnAnchorId = Guid.Parse("dddd0001-0000-0000-0000-000000000001");
    private static readonly Guid OffAnchorId = Guid.Parse("dddd0002-0000-0000-0000-000000000002");

    private static (DueDateApproachingJob Job, ILocalEventBus Bus) Build(
        bool enabled, params Appointment[] appointments)
    {
        var bus = Substitute.For<ILocalEventBus>();
        var job = new DueDateApproachingJob(
            ReminderJobTestHarness.AppointmentRepo(appointments),
            ReminderJobTestHarness.NoopDataFilter(),
            ReminderJobTestHarness.NoopCurrentTenant(),
            bus,
            ReminderJobTestHarness.Settings(
                enabled,
                CaseEvaluationSettings.RemindersPolicy.DueDateApproachingAnchors,
                "14,7,3"),
            NullLogger<DueDateApproachingJob>.Instance);

        return (job, bus);
    }

    private static Appointment WithDueDate(Guid id, DateTime dueDate)
    {
        var appt = ReminderJobTestHarness.Appt(id, AppointmentStatusType.Approved, dueDate);
        appt.DueDate = dueDate;
        return appt;
    }

    [Fact]
    public async Task Muted_when_reminders_disabled()
    {
        var today = DateTime.UtcNow.Date;
        var (job, bus) = Build(false, WithDueDate(OnAnchorId, today.AddDays(14)));

        await job.ExecuteAsync();

        await bus.DidNotReceive().PublishAsync(Arg.Any<DueDateApproachingEto>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task Fires_only_for_anchor_days_when_enabled()
    {
        var today = DateTime.UtcNow.Date;
        var (job, bus) = Build(
            true,
            WithDueDate(OnAnchorId, today.AddDays(14)),
            WithDueDate(OffAnchorId, today.AddDays(10)));

        await job.ExecuteAsync();

        await bus.Received(1).PublishAsync(
            Arg.Is<DueDateApproachingEto>(e => e.AppointmentId == OnAnchorId), Arg.Any<bool>());
        await bus.DidNotReceive().PublishAsync(
            Arg.Is<DueDateApproachingEto>(e => e.AppointmentId == OffAnchorId), Arg.Any<bool>());
    }
}
