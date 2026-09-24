using System.Collections.Generic;
using System.Linq;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications.Jobs;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Volo.Abp.BackgroundJobs;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Notifications.Jobs;

/// <summary>
/// What the three reminder jobs actually SEND. Their own test classes resolve no recipients, so the
/// enqueue loop never runs there. Here each on-anchor appointment has two parties, and each must get
/// exactly one queued email carrying the job's subject, an HTML body and a context naming the job,
/// the party's role and the appointment. An off-anchor appointment sits in every fixture as the decoy
/// that must queue nothing. All data is synthetic.
/// </summary>
public class ReminderJobEnqueueTests
{
    private static readonly Guid OnAnchorId = Guid.Parse("bbbb0001-0000-4000-8000-000000000001");
    private static readonly Guid OffAnchorId = Guid.Parse("bbbb0002-0000-4000-8000-000000000002");

    private sealed class Harness
    {
        public IBackgroundJobManager Jobs { get; } = Substitute.For<IBackgroundJobManager>();

        public IAppointmentRecipientResolver Resolver { get; } = Substitute.For<IAppointmentRecipientResolver>();

        /// <summary>The two parties every resolved appointment has; the job fills in the rest.</summary>
        public List<SendAppointmentEmailArgs> Parties { get; } = new()
        {
            new SendAppointmentEmailArgs { To = "TEST-reminder-patient@test.local", Role = RecipientRole.Patient },
            new SendAppointmentEmailArgs { To = "TEST-reminder-attorney@test.local", Role = RecipientRole.ApplicantAttorney },
        };

        public Harness()
        {
            Resolver.ResolveAsync(OnAnchorId, Arg.Any<NotificationKind>()).Returns(Parties);
            Resolver.ResolveAsync(OffAnchorId, Arg.Any<NotificationKind>())
                .Returns(new List<SendAppointmentEmailArgs>
                {
                    new() { To = "TEST-reminder-decoy@test.local", Role = RecipientRole.Patient },
                });
        }

        public List<SendAppointmentEmailArgs> Queued() =>
            Jobs.ReceivedCalls()
                .Select(call => call.GetArguments()[0])
                .OfType<SendAppointmentEmailArgs>()
                .ToList();
    }

    [Fact]
    public async Task AppointmentDayReminder_QueuesOneEmailPerParty_ForTheAnchorDayOnly()
    {
        var h = new Harness();
        var today = ReminderJobTestHarness.PacificToday;
        var onAnchor = ReminderJobTestHarness.Appt(OnAnchorId, AppointmentStatusType.Approved, today.AddDays(7));
        var job = new AppointmentDayReminderJob(
            ReminderJobTestHarness.AppointmentRepo(
                onAnchor,
                ReminderJobTestHarness.Appt(OffAnchorId, AppointmentStatusType.Approved, today.AddDays(5))),
            ReminderJobTestHarness.TenantWorkRunner(),
            h.Resolver,
            h.Jobs,
            ReminderJobTestHarness.Settings(true, CaseEvaluationSettings.RemindersPolicy.AppointmentDayTMinusAnchors, "7,1"),
            NullLogger<AppointmentDayReminderJob>.Instance,
            ReminderJobTestHarness.Clock());

        await job.ExecuteAsync();

        var queued = h.Queued();
        queued.Select(q => q.To).ShouldBe(h.Parties.Select(p => p.To), ignoreOrder: true);
        foreach (var email in queued)
        {
            email.Subject.ShouldBe($"Reminder: appointment {onAnchor.RequestConfirmationNumber} in 7 days");
            email.Body.ShouldContain(onAnchor.RequestConfirmationNumber!);
            email.IsBodyHtml.ShouldBeTrue();
            email.Context.ShouldBe($"Reminder/AppointmentDay/T-7/{email.Role}/{OnAnchorId}");
        }
    }

    [Fact]
    public async Task CancellationRescheduleReminder_QueuesOneEmailPerParty_ForTheElapsedAnchorOnly()
    {
        var h = new Harness();
        var since = ReminderJobTestHarness.NowUtc;
        var onAnchor = ModifiedOn(OnAnchorId, AppointmentStatusType.CancellationRequested, since.AddDays(-45));
        var job = new CancellationRescheduleReminderJob(
            ReminderJobTestHarness.AppointmentRepo(
                onAnchor,
                ModifiedOn(OffAnchorId, AppointmentStatusType.CancellationRequested, since.AddDays(-46))),
            ReminderJobTestHarness.TenantWorkRunner(),
            h.Resolver,
            h.Jobs,
            ReminderJobTestHarness.Settings(true, CaseEvaluationSettings.RemindersPolicy.Sec34eElapsedDayAnchors, "45,55"),
            NullLogger<CancellationRescheduleReminderJob>.Instance,
            ReminderJobTestHarness.Clock());

        await job.ExecuteAsync();

        var queued = h.Queued();
        queued.Select(q => q.To).ShouldBe(h.Parties.Select(p => p.To), ignoreOrder: true);
        foreach (var email in queued)
        {
            email.Subject.ShouldBe($"Reminder: cancellation/reschedule clock running for {onAnchor.RequestConfirmationNumber}");
            email.Body.ShouldContain(onAnchor.RequestConfirmationNumber!);
            email.IsBodyHtml.ShouldBeTrue();
            email.Context.ShouldBe($"Reminder/Sec34e/{email.Role}/{OnAnchorId}");
        }
    }

    [Fact]
    public async Task RequestSchedulingReminder_QueuesOneEmailPerParty_ForTheElapsedAnchorOnly()
    {
        var h = new Harness();
        var since = ReminderJobTestHarness.NowUtc;
        var onAnchor = CreatedOn(OnAnchorId, since.AddDays(-30));
        var job = new RequestSchedulingReminderJob(
            ReminderJobTestHarness.AppointmentRepo(onAnchor, CreatedOn(OffAnchorId, since.AddDays(-31))),
            ReminderJobTestHarness.TenantWorkRunner(),
            h.Resolver,
            h.Jobs,
            ReminderJobTestHarness.Settings(true, CaseEvaluationSettings.RemindersPolicy.Sec31_5ElapsedDayAnchors, "30,60,75,85,90"),
            NullLogger<RequestSchedulingReminderJob>.Instance,
            ReminderJobTestHarness.Clock());

        await job.ExecuteAsync();

        var queued = h.Queued();
        queued.Select(q => q.To).ShouldBe(h.Parties.Select(p => p.To), ignoreOrder: true);
        foreach (var email in queued)
        {
            email.Subject.ShouldBe($"Reminder: appointment request {onAnchor.RequestConfirmationNumber} still pending");
            email.Body.ShouldContain(onAnchor.RequestConfirmationNumber!);
            email.IsBodyHtml.ShouldBeTrue();
            email.Context.ShouldBe($"Reminder/Sec31.5/{email.Role}/{OnAnchorId}");
        }
    }

    // ------------------------------------------------------------------------

    private static Appointment ModifiedOn(Guid id, AppointmentStatusType status, DateTime modifiedOn)
    {
        var appointment = ReminderJobTestHarness.Appt(id, status, modifiedOn);
        appointment.LastModificationTime = modifiedOn;
        return appointment;
    }

    private static Appointment CreatedOn(Guid id, DateTime createdOn)
    {
        var appointment = ReminderJobTestHarness.Appt(id, AppointmentStatusType.Pending, createdOn);
        appointment.CreationTime = createdOn;
        return appointment;
    }
}
