using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.MultiTenancy;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using HealthcareSupport.CaseEvaluation.Settings;
using HealthcareSupport.CaseEvaluation.Timing;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Local;
using Volo.Abp.Settings;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Notifications.Jobs;

/// <summary>
/// Unit coverage for <see cref="AppointmentReminderJob"/>, the 08:15 daily sweep that raises one
/// reminder event per appointment whose due date is exactly an "anchor" number of days away.
///
/// <para><b>WHAT IS PINNED.</b> Nothing fires when reminders are switched off for the office; an
/// event fires only on an anchor day; only live statuses (Pending, Approved, RescheduleRequested)
/// qualify; an appointment with no due date is skipped; and malformed anchors fire nothing.</para>
///
/// <para><b>THE CLOCK, and its one known edge.</b> The job reads the real clock
/// (<c>PacificTime.TodayFrom(DateTime.UtcNow)</c>); it takes no <c>IClock</c>. These Facts compute
/// "today" the same way just before running it. If a run straddles Pacific midnight, the two
/// readings can differ by one day and an anchor Fact would miss -- a window of milliseconds once a
/// day. Stated rather than hidden.</para>
///
/// <para><b>The result asserted is the published <see cref="AppointmentReminderEto"/></b>, the job's
/// only output. No database: the repository returns an in-memory list.</para>
///
/// <para>Synthetic data only (HIPAA).</para>
/// </summary>
public class AppointmentReminderJobTests
{
    private static readonly Guid OfficeId = new("44444444-4444-4444-4444-444444444444");

    private sealed class Harness
    {
        public List<Appointment> Appointments { get; } = new();
        public ISettingProvider Settings { get; } = Substitute.For<ISettingProvider>();
        public IRepository<Appointment, Guid> AppointmentRepository { get; } = Substitute.For<IRepository<Appointment, Guid>>();
        public ILocalEventBus Bus { get; } = Substitute.For<ILocalEventBus>();
        public AppointmentReminderJob Job { get; }

        public Harness(string enabled = "true", string? anchors = "7,3")
        {
            AppointmentRepository.GetQueryableAsync().Returns(_ => Appointments.AsQueryable());
            // GetAsync<bool> is an extension over GetOrNullAsync, so the interface member is what a
            // substitute intercepts (same note as JointDeclarationOverdueJobTests).
            Settings.GetOrNullAsync(CaseEvaluationSettings.RemindersPolicy.RemindersEnabled).Returns(enabled);
            Settings.GetOrNullAsync(CaseEvaluationSettings.RemindersPolicy.DueDateApproachingAnchors).Returns(anchors);
            var tenantRunner = Substitute.For<ITenantWorkRunner>();
            tenantRunner.ForEachOfficeAsync(Arg.Any<Func<Guid, Task>>())
                .Returns(ci => ci.Arg<Func<Guid, Task>>()(OfficeId));

            Job = new AppointmentReminderJob(
                AppointmentRepository,
                tenantRunner,
                Bus,
                Settings,
                NullLogger<AppointmentReminderJob>.Instance);
        }

        public List<AppointmentReminderEto> Reminders() =>
            Bus.ReceivedCalls().SelectMany(c => c.GetArguments()).OfType<AppointmentReminderEto>().ToList();
    }

    /// <summary>Pacific "today", computed the way the job computes it.</summary>
    private static DateTime PacificToday() => PacificTime.TodayFrom(DateTime.UtcNow);

    private static Appointment NewAppointment(AppointmentStatusType status, DateTime? dueDate) => new(
        id: Guid.NewGuid(),
        patientId: Guid.NewGuid(),
        identityUserId: null,
        appointmentTypeId: Guid.NewGuid(),
        locationId: Guid.NewGuid(),
        doctorAvailabilityId: Guid.NewGuid(),
        appointmentDate: new DateTime(2026, 12, 1, 9, 0, 0),
        requestConfirmationNumber: "TEST-R0001",
        appointmentStatus: status,
        dueDate: dueDate);

    /// <summary>
    /// <b>POSITIVE CONTROL.</b> An appointment due exactly one anchor away raises one reminder with the
    /// right day count. The "fires nothing" Facts below depend on this one reaching the bus.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_DueOnAnAnchorDay_RaisesOneReminder_PositiveControl()
    {
        var h = new Harness();
        var appointment = NewAppointment(AppointmentStatusType.Approved, PacificToday().AddDays(7));
        h.Appointments.Add(appointment);

        await h.Job.ExecuteAsync();

        var reminder = h.Reminders().ShouldHaveSingleItem();
        reminder.AppointmentId.ShouldBe(appointment.Id);
        reminder.TenantId.ShouldBe(OfficeId);
        reminder.DaysUntilDue.ShouldBe(7);
    }

    [Fact]
    public async Task ExecuteAsync_RemindersSwitchedOffForTheOffice_RaisesNothingAndReadsNoAppointments()
    {
        var h = new Harness(enabled: "false");
        h.Appointments.Add(NewAppointment(AppointmentStatusType.Approved, PacificToday().AddDays(7)));

        await h.Job.ExecuteAsync();

        h.Reminders().ShouldBeEmpty("an office with reminders switched off must get none");
        await h.AppointmentRepository.DidNotReceive().GetQueryableAsync();
    }

    [Fact]
    public async Task ExecuteAsync_ADayThatIsNotAnAnchor_RaisesNothing()
    {
        var h = new Harness();
        h.Appointments.Add(NewAppointment(AppointmentStatusType.Approved, PacificToday().AddDays(5)));

        await h.Job.ExecuteAsync();

        h.Reminders().ShouldBeEmpty("anchors are 7 and 3; five days out is neither");
    }

    /// <summary>
    /// Only live requests are reminded: a cancelled, rejected or finished appointment that happens to
    /// land on an anchor day must not produce a reminder.
    /// </summary>
    [Theory]
    [InlineData(AppointmentStatusType.Pending, true)]
    [InlineData(AppointmentStatusType.Approved, true)]
    [InlineData(AppointmentStatusType.RescheduleRequested, true)]
    [InlineData(AppointmentStatusType.CancelledNoBill, false)]
    [InlineData(AppointmentStatusType.Rejected, false)]
    [InlineData(AppointmentStatusType.CheckedOut, false)]
    public async Task ExecuteAsync_OnlyLiveStatusesAreReminded(AppointmentStatusType status, bool expectReminder)
    {
        var h = new Harness();
        h.Appointments.Add(NewAppointment(status, PacificToday().AddDays(3)));

        await h.Job.ExecuteAsync();

        h.Reminders().Count.ShouldBe(expectReminder ? 1 : 0);
    }

    [Fact]
    public async Task ExecuteAsync_AnAppointmentWithNoDueDate_IsSkipped()
    {
        var h = new Harness();
        h.Appointments.Add(NewAppointment(AppointmentStatusType.Approved, dueDate: null));
        h.Appointments.Add(NewAppointment(AppointmentStatusType.Approved, PacificToday().AddDays(3)));

        await h.Job.ExecuteAsync();

        h.Reminders().ShouldHaveSingleItem().DaysUntilDue.ShouldBe(3);
    }

    /// <summary>
    /// Anchors come from a free-text setting. Garbage, negatives and an empty value parse to NO anchors,
    /// so the job fires nothing rather than throwing.
    /// </summary>
    [Theory]
    [InlineData("TEST-not-a-number, -7, ")]
    [InlineData("")]
    [InlineData(null)]
    public async Task ExecuteAsync_MalformedOrEmptyAnchors_RaiseNothing(string? anchors)
    {
        var h = new Harness(anchors: anchors);
        h.Appointments.Add(NewAppointment(AppointmentStatusType.Approved, PacificToday().AddDays(7)));

        await h.Job.ExecuteAsync();

        h.Reminders().ShouldBeEmpty("with no valid anchor there is no day on which to remind");
    }
}
