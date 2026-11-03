using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications.Jobs;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using HealthcareSupport.CaseEvaluation.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Volo.Abp.EventBus.Local;
using Volo.Abp.Settings;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Notifications.Jobs;

/// <summary>
/// Wiring tests for <see cref="AppointmentDayReminderJob"/> (2026-11-03, email routing row 15). The job
/// no longer builds or queues email: it raises one <see cref="AppointmentDayReminderEto"/> per Approved
/// appointment whose date is 7 or 1 days away, and the email handler sends the editable template. The
/// job is gated by its OWN switch (<c>AppointmentDayReminderEnabled</c>, default on), never by the global
/// <c>RemindersEnabled</c> (default off) that the other three reminder jobs still honor. Cadence parsing
/// is covered by <see cref="ReminderCadenceTests"/>.
/// </summary>
public class AppointmentDayReminderJobTests
{
    private static readonly Guid OnAnchorId = Guid.Parse("aaaa0001-0000-0000-0000-000000000001");
    private static readonly Guid TomorrowId = Guid.Parse("aaaa0003-0000-0000-0000-000000000003");
    private static readonly Guid OffAnchorId = Guid.Parse("aaaa0002-0000-0000-0000-000000000002");

    private static (AppointmentDayReminderJob Job, ILocalEventBus Bus) Build(
        string daySwitch, string globalSwitch, params Appointment[] appointments)
    {
        var settings = Substitute.For<ISettingProvider>();
        settings.GetOrNullAsync(CaseEvaluationSettings.RemindersPolicy.AppointmentDayReminderEnabled).Returns(daySwitch);
        settings.GetOrNullAsync(CaseEvaluationSettings.RemindersPolicy.RemindersEnabled).Returns(globalSwitch);
        settings.GetOrNullAsync(CaseEvaluationSettings.RemindersPolicy.AppointmentDayTMinusAnchors).Returns("7,1");

        var bus = Substitute.For<ILocalEventBus>();
        var job = new AppointmentDayReminderJob(
            ReminderJobTestHarness.AppointmentRepo(appointments),
            ReminderJobTestHarness.TenantWorkRunner(),
            bus,
            settings,
            NullLogger<AppointmentDayReminderJob>.Instance,
            ReminderJobTestHarness.Clock());
        return (job, bus);
    }

    private static List<AppointmentDayReminderEto> Raised(ILocalEventBus bus) =>
        bus.ReceivedCalls().SelectMany(c => c.GetArguments()).OfType<AppointmentDayReminderEto>().ToList();

    [Fact]
    public async Task Muted_when_the_appointment_day_switch_is_off_even_if_the_global_switch_is_on()
    {
        var today = ReminderJobTestHarness.PacificToday;
        var (job, bus) = Build(
            daySwitch: "false",
            globalSwitch: "true",
            ReminderJobTestHarness.Appt(OnAnchorId, AppointmentStatusType.Approved, today.AddDays(7)));

        await job.ExecuteAsync();

        Raised(bus).ShouldBeEmpty();
    }

    /// <summary>
    /// THE decision: the office wants only this reminder on. With the global switch at its default
    /// (off) and the appointment-day switch on, the 7-day and the 1-day appointment each raise one event
    /// and the off-anchor decoy raises none.
    /// </summary>
    [Fact]
    public async Task Fires_for_the_7_day_and_1_day_anchors_when_only_the_day_switch_is_on()
    {
        var today = ReminderJobTestHarness.PacificToday;
        var (job, bus) = Build(
            daySwitch: "true",
            globalSwitch: "false",
            ReminderJobTestHarness.Appt(OnAnchorId, AppointmentStatusType.Approved, today.AddDays(7)),
            ReminderJobTestHarness.Appt(TomorrowId, AppointmentStatusType.Approved, today.AddDays(1)),
            ReminderJobTestHarness.Appt(OffAnchorId, AppointmentStatusType.Approved, today.AddDays(5)));

        await job.ExecuteAsync();

        var raised = Raised(bus);
        raised.Select(e => (e.AppointmentId, e.DaysUntil)).ShouldBe(
            new[] { (OnAnchorId, 7), (TomorrowId, 1) }, ignoreOrder: true);
        raised.ShouldAllBe(e => e.TenantId == ReminderJobTestHarness.TenantId);
    }

    [Fact]
    public async Task Only_approved_appointments_get_a_reminder()
    {
        var today = ReminderJobTestHarness.PacificToday;
        var (job, bus) = Build(
            daySwitch: "true",
            globalSwitch: "false",
            ReminderJobTestHarness.Appt(OnAnchorId, AppointmentStatusType.Pending, today.AddDays(7)),
            ReminderJobTestHarness.Appt(TomorrowId, AppointmentStatusType.CancelledNoBill, today.AddDays(1)));

        await job.ExecuteAsync();

        Raised(bus).ShouldBeEmpty();
    }

    [Fact]
    public void The_day_switch_defaults_on_and_the_global_reminder_switch_stays_off()
    {
        var added = new List<SettingDefinition>();
        var context = Substitute.For<ISettingDefinitionContext>();
        context.When(c => c.Add(Arg.Any<SettingDefinition[]>()))
            .Do(call => added.AddRange(call.Arg<SettingDefinition[]>()));

        new CaseEvaluationSettingDefinitionProvider(new ConfigurationBuilder().Build()).Define(context);

        added.Single(d => d.Name == CaseEvaluationSettings.RemindersPolicy.AppointmentDayReminderEnabled)
            .DefaultValue.ShouldBe("true");
        added.Single(d => d.Name == CaseEvaluationSettings.RemindersPolicy.RemindersEnabled)
            .DefaultValue.ShouldBe("false");
    }
}
