using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Settings;
using Volo.Abp.Timing;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Notifications.Outbox;

/// <summary>
/// T10 unit tests for <see cref="OutboxDrainService"/>: the no-loss + no-duplicate
/// delivery guarantees. Uses a real <see cref="NotificationOutboxManager"/> over a
/// List-backed mock repository so claim/mark actually mutate the ledger and a
/// second drain observes the real post-send state; the SMTP transport and clock
/// are mocked.
/// </summary>
public class OutboxDrainServiceTests
{
    private static readonly Guid TenantId = new("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
    private static readonly DateTime Now = new(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc);

    private sealed class Harness
    {
        public required List<NotificationOutboxItem> Rows { get; init; }
        public required NotificationOutboxManager Manager { get; init; }
        public required IOutboxEmailSender Sender { get; init; }
        public required OutboxDrainService Service { get; init; }
    }

    private static Harness Build(bool emailEnabled = true)
    {
        var rows = new List<NotificationOutboxItem>();
        var repo = Substitute.For<IRepository<NotificationOutboxItem, Guid>>();
        repo.GetQueryableAsync().Returns(_ => rows.AsQueryable());
        repo.InsertAsync(Arg.Any<NotificationOutboxItem>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var item = ci.Arg<NotificationOutboxItem>();
                rows.Add(item);
                return Task.FromResult(item);
            });
        repo.UpdateAsync(Arg.Any<NotificationOutboxItem>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(ci.Arg<NotificationOutboxItem>()));

        var manager = new NotificationOutboxManager(repo, SimpleGuidGenerator.Instance);
        var sender = Substitute.For<IOutboxEmailSender>();
        var clock = Substitute.For<IClock>();
        clock.Now.Returns(Now);
        var settingProvider = Substitute.For<ISettingProvider>();
        settingProvider.GetOrNullAsync(CaseEvaluationSettings.NotificationsPolicy.EmailEnabled)
            .Returns(emailEnabled ? "true" : "false");
        var service = new OutboxDrainService(manager, sender, clock, settingProvider, NullLogger<OutboxDrainService>.Instance);

        return new Harness { Rows = rows, Manager = manager, Sender = sender, Service = service };
    }

    private static Task SeedPendingAsync(Harness h, string key = "key-1") =>
        h.Manager.EnqueueAsync(TenantId, "party@example.test", null, "Subject", "<p>body</p>", true, "Approved/appt-1", key);

    [Fact]
    public async Task DrainDueAsync_SendsDueRow_AndMarksSent()
    {
        var h = Build();
        await SeedPendingAsync(h);

        var result = await h.Service.DrainDueAsync();

        result.Sent.ShouldBe(1);
        result.Failed.ShouldBe(0);
        await h.Sender.Received(1).SendAsync(Arg.Is<SendAppointmentEmailArgs>(a => a.To == "party@example.test"));
        h.Rows.Single().Status.ShouldBe(NotificationOutboxStatus.Sent);
    }

    [Fact]
    public async Task DrainDueAsync_WhenSendThrows_MarksFailed_NotLost_AndRedrivable()
    {
        var h = Build();
        await SeedPendingAsync(h);
        h.Sender
            .When(s => s.SendAsync(Arg.Any<SendAppointmentEmailArgs>()))
            .Do(_ => throw new BusinessException("smtp-down"));

        var result = await h.Service.DrainDueAsync();

        result.Sent.ShouldBe(0);
        result.Failed.ShouldBe(1);
        var row = h.Rows.Single();
        // Not lost: still present, below the cap it returns to Pending with a backoff.
        row.Status.ShouldBe(NotificationOutboxStatus.Pending);
        row.AttemptCount.ShouldBe(1);
        row.NextAttemptAt.ShouldNotBeNull();
        row.SentAt.ShouldBeNull();
    }

    [Fact]
    public async Task DrainDueAsync_WhenEmailDisabled_HoldsRowsPending_AndSendsNothing()
    {
        // #4a master switch OFF: no send, and the row stays Pending with no attempt
        // increment so it resumes cleanly (not dead-lettered) once re-enabled.
        var h = Build(emailEnabled: false);
        await SeedPendingAsync(h);

        var result = await h.Service.DrainDueAsync();

        result.Sent.ShouldBe(0);
        result.Failed.ShouldBe(0);
        await h.Sender.DidNotReceive().SendAsync(Arg.Any<SendAppointmentEmailArgs>());
        var row = h.Rows.Single();
        row.Status.ShouldBe(NotificationOutboxStatus.Pending);
        row.AttemptCount.ShouldBe(0);
        row.SentAt.ShouldBeNull();
    }

    [Fact]
    public async Task DrainDueAsync_SecondPass_DoesNotResendASentRow()
    {
        var h = Build();
        await SeedPendingAsync(h);

        await h.Service.DrainDueAsync();  // sends + marks Sent
        await h.Service.DrainDueAsync();  // the row is Sent now -> not claimable

        // Exactly one send across both passes: no duplicate PHI email.
        await h.Sender.Received(1).SendAsync(Arg.Any<SendAppointmentEmailArgs>());
        h.Rows.Single().Status.ShouldBe(NotificationOutboxStatus.Sent);
    }
}
