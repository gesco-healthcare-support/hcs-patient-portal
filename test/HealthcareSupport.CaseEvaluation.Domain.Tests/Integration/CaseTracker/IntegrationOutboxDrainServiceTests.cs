using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Volo.Abp.Guids;
using Volo.Abp.Settings;
using Volo.Abp.Timing;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Unit tests for <see cref="IntegrationOutboxDrainService"/>: the delivery guarantees that keep a
/// case from being silently lost. Uses a real <see cref="IntegrationOutboxManager"/> over a
/// List-backed mock repository so claim/mark actually mutate the ledger, mirroring
/// <c>OutboxDrainServiceTests</c>; the HTTP client, clock and settings are mocked.
/// </summary>
public class IntegrationOutboxDrainServiceTests
{
    private static readonly Guid TenantId = new("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
    private static readonly Guid AppointmentId = new("8f14e45f-ceea-467a-9f3a-1a2b3c4d5e6f");
    private static readonly DateTime Now = new(2026, 7, 27, 12, 0, 0, DateTimeKind.Utc);

    private sealed class Harness
    {
        public required List<IntegrationOutboxItem> Rows { get; init; }
        public required IntegrationOutboxManager Manager { get; init; }
        public required ICaseTrackerClient Client { get; init; }
        public required IntegrationOutboxDrainService Service { get; init; }
    }

    private static Harness Build(bool pushEnabled = true)
    {
        var rows = new List<IntegrationOutboxItem>();
        var repo = Substitute.For<IIntegrationOutboxRepository>();
        repo.GetQueryableAsync().Returns(_ => rows.AsQueryable());
        repo.InsertAsync(Arg.Any<IntegrationOutboxItem>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var item = ci.Arg<IntegrationOutboxItem>();
                rows.Add(item);
                return Task.FromResult(item);
            });
        repo.UpdateAsync(Arg.Any<IntegrationOutboxItem>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(ci.Arg<IntegrationOutboxItem>()));
        // Emulate the DB-atomic lease over the in-memory rows; TryClaim enforces the same gate the
        // SQL UPDATE would, so a second drain observes the real post-send state.
        repo.TryLeaseAsync(Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var row = rows.FirstOrDefault(r => r.Id == ci.ArgAt<Guid>(0));
                var now = ci.ArgAt<DateTime>(1);
                var leaseUntil = ci.ArgAt<DateTime>(2);
                return Task.FromResult(row != null && row.TryClaim(now, leaseUntil - now));
            });
        repo.GetAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(rows.First(r => r.Id == ci.ArgAt<Guid>(0))));

        var manager = new IntegrationOutboxManager(repo, SimpleGuidGenerator.Instance);
        var client = Substitute.For<ICaseTrackerClient>();
        var clock = Substitute.For<IClock>();
        clock.Now.Returns(Now);
        var settingProvider = Substitute.For<ISettingProvider>();
        settingProvider.GetOrNullAsync(CaseEvaluationSettings.IntegrationPolicy.CaseTrackerPushEnabled)
            .Returns(pushEnabled ? "true" : "false");

        var service = new IntegrationOutboxDrainService(
            manager, client, clock, settingProvider, NullLogger<IntegrationOutboxDrainService>.Instance);

        return new Harness { Rows = rows, Manager = manager, Client = client, Service = service };
    }

    private static Task SeedPendingAsync(Harness h, string key = "key-1") =>
        h.Manager.EnqueueAsync(
            TenantId,
            IntegrationMessageType.Intake,
            CaseTrackerEndpoints.Intake,
            AppointmentId,
            "{\"data\":{}}",
            key);

    [Fact]
    public async Task DrainDueAsync_OnSuccess_MarksSent()
    {
        var h = Build();
        await SeedPendingAsync(h);
        h.Client.PostAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(CaseTrackerPushResult.FromStatusCode(200));

        var result = await h.Service.DrainDueAsync();

        result.Sent.ShouldBe(1);
        result.Failed.ShouldBe(0);
        h.Rows.Single().Status.ShouldBe(IntegrationOutboxStatus.Sent);
        h.Rows.Single().SentAt.ShouldBe(Now);
    }

    [Fact]
    public async Task DrainDueAsync_PostsToTheRowsTargetPath()
    {
        var h = Build();
        await SeedPendingAsync(h);
        h.Client.PostAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(CaseTrackerPushResult.FromStatusCode(204));

        await h.Service.DrainDueAsync();

        await h.Client.Received(1).PostAsync(
            CaseTrackerEndpoints.Intake, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DrainDueAsync_OnRetryableFailure_ReschedulesWithBackoff_AndIsNotLost()
    {
        var h = Build();
        await SeedPendingAsync(h);
        h.Client.PostAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(CaseTrackerPushResult.FromStatusCode(503));

        var result = await h.Service.DrainDueAsync();

        result.Failed.ShouldBe(1);
        var row = h.Rows.Single();
        row.Status.ShouldBe(IntegrationOutboxStatus.Pending);
        row.AttemptCount.ShouldBe(1);
        row.NextAttemptAt.ShouldBe(Now.AddSeconds(IntegrationOutboxConsts.RetryBackoffSeconds));
        row.SentAt.ShouldBeNull();
    }

    [Fact]
    public async Task DrainDueAsync_OnFatalFailure_DeadLettersImmediately_WithoutBurningTheCap()
    {
        // A 401 can never succeed on retry, so it must not consume three attempts before a human is
        // told -- the whole point of the fail-fast policy.
        var h = Build();
        await SeedPendingAsync(h);
        h.Client.PostAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(CaseTrackerPushResult.FromStatusCode(401));

        var result = await h.Service.DrainDueAsync();

        result.Failed.ShouldBe(1);
        var row = h.Rows.Single();
        row.Status.ShouldBe(IntegrationOutboxStatus.Failed);
        row.AttemptCount.ShouldBe(1);
        row.NextAttemptAt.ShouldBeNull();
    }

    [Fact]
    public async Task DrainDueAsync_WhenPushDisabled_HoldsRowsPending_AndPostsNothing()
    {
        // Master switch OFF: no request, and the row keeps a clean slate so it resumes rather than
        // dead-letters once an office is enabled.
        var h = Build(pushEnabled: false);
        await SeedPendingAsync(h);

        var result = await h.Service.DrainDueAsync();

        result.Sent.ShouldBe(0);
        result.Failed.ShouldBe(0);
        await h.Client.DidNotReceive().PostAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        var row = h.Rows.Single();
        row.Status.ShouldBe(IntegrationOutboxStatus.Pending);
        row.AttemptCount.ShouldBe(0);
    }

    [Fact]
    public async Task DrainDueAsync_WhenOneRowThrows_StillProcessesTheRest()
    {
        var h = Build();
        await SeedPendingAsync(h, "key-1");
        await SeedPendingAsync(h, "key-2");

        var calls = 0;
        h.Client.PostAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                calls++;
                if (calls == 1)
                {
                    throw new InvalidOperationException("unexpected");
                }
                return Task.FromResult(CaseTrackerPushResult.FromStatusCode(200));
            });

        var result = await h.Service.DrainDueAsync();

        // The thrower is recorded as a retryable failure; the sibling still went out.
        result.Failed.ShouldBe(1);
        result.Sent.ShouldBe(1);
        h.Rows.Count(r => r.Status == IntegrationOutboxStatus.Sent).ShouldBe(1);
        h.Rows.Count(r => r.Status == IntegrationOutboxStatus.Pending).ShouldBe(1);
    }
}
