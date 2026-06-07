using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Notifications;

/// <summary>
/// E1 (2026-06-03) -- unit tests for the CC plumbing in
/// <see cref="NotificationDispatcher.DispatchToWithCcAsync"/>: render once,
/// enqueue exactly ONE email job addressed To the primary with the rest CC'd
/// (the To address + duplicates dropped).
/// </summary>
public class NotificationDispatcherCcUnitTests
{
    private static (NotificationDispatcher dispatcher, IBackgroundJobManager jobs) Build()
    {
        var renderer = Substitute.For<INotificationTemplateRenderer>();
        renderer
            .RenderAsync(Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, object?>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new RenderedNotification("Subj", "Body", null)));

        var jobs = Substitute.For<IBackgroundJobManager>();

        var tenant = Substitute.For<ICurrentTenant>();
        tenant.Name.Returns("Falkinstein");
        tenant.Id.Returns((Guid?)null);

        var dispatcher = new NotificationDispatcher(
            renderer, jobs, tenant, NullLogger<NotificationDispatcher>.Instance);
        return (dispatcher, jobs);
    }

    private static SendAppointmentEmailArgs CapturedArgs(IBackgroundJobManager jobs)
    {
        var call = jobs.ReceivedCalls()
            .Single(c => c.GetMethodInfo().Name == nameof(IBackgroundJobManager.EnqueueAsync));
        return (SendAppointmentEmailArgs)call.GetArguments()[0]!;
    }

    [Fact]
    public async Task DispatchToWithCc_EnqueuesOneJob_ToPrimary_CcRest_ToExcludedAndDeduped()
    {
        var (dispatcher, jobs) = Build();
        var to = new NotificationRecipient("booker@gesco.com", role: RecipientRole.Patient, isRegistered: true);
        var cc = new[]
        {
            new NotificationRecipient("aa@gesco.com", role: RecipientRole.ApplicantAttorney),
            new NotificationRecipient("da@gesco.com", role: RecipientRole.DefenseAttorney),
            new NotificationRecipient("booker@gesco.com", role: RecipientRole.Patient), // == To -> dropped
            new NotificationRecipient("AA@gesco.com", role: RecipientRole.ApplicantAttorney), // case-dup -> dropped
        };

        await dispatcher.DispatchToWithCcAsync(
            "AppointmentRequested", to, cc, new Dictionary<string, object?>(), "ctx/1");

        await jobs.Received(1).EnqueueAsync(
            Arg.Any<SendAppointmentEmailArgs>(), Arg.Any<BackgroundJobPriority>(), Arg.Any<TimeSpan?>());

        var args = CapturedArgs(jobs);
        args.To.ShouldBe("booker@gesco.com");
        args.Subject.ShouldBe("Subj");
        args.Body.ShouldBe("Body");
        args.Cc.Count.ShouldBe(2);
        args.Cc.ShouldContain("aa@gesco.com");
        args.Cc.ShouldContain("da@gesco.com");
        args.Cc.ShouldNotContain("booker@gesco.com");
    }

    [Fact]
    public async Task DispatchToWithCc_EmptyTo_DoesNotEnqueue()
    {
        var (dispatcher, jobs) = Build();
        var to = new NotificationRecipient(string.Empty, role: RecipientRole.Patient);

        await dispatcher.DispatchToWithCcAsync(
            "AppointmentRequested", to, Array.Empty<NotificationRecipient>(),
            new Dictionary<string, object?>(), "ctx/1");

        await jobs.DidNotReceive().EnqueueAsync(
            Arg.Any<SendAppointmentEmailArgs>(), Arg.Any<BackgroundJobPriority>(), Arg.Any<TimeSpan?>());
    }
}
