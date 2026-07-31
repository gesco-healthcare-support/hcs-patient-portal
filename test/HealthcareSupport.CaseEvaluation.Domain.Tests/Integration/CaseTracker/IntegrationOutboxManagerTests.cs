using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Shouldly;
using Volo.Abp.Guids;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Unit tests for <see cref="IntegrationOutboxManager"/>: the idempotent enqueue that stops a
/// redelivered approval event from pushing the same case twice, and the due-batch claim.
/// </summary>
public class IntegrationOutboxManagerTests
{
    private static readonly Guid TenantId = new("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
    private static readonly Guid AppointmentId = new("8f14e45f-ceea-467a-9f3a-1a2b3c4d5e6f");
    private static readonly DateTime Now = new(2026, 7, 27, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Lease = TimeSpan.FromSeconds(IntegrationOutboxConsts.LeaseDurationSeconds);

    private static (IntegrationOutboxManager Manager, List<IntegrationOutboxItem> Rows) Build()
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

        return (new IntegrationOutboxManager(repo, SimpleGuidGenerator.Instance), rows);
    }

    private static Task<IntegrationOutboxItem> EnqueueAsync(IntegrationOutboxManager manager, string key) =>
        manager.EnqueueAsync(
            TenantId,
            IntegrationMessageType.Intake,
            CaseTrackerEndpoints.Intake,
            AppointmentId,
            "{\"data\":{}}",
            key);

    [Fact]
    public async Task EnqueueAsync_InsertsAPendingRow()
    {
        var (manager, rows) = Build();

        var row = await EnqueueAsync(manager, "key-1");

        rows.Count.ShouldBe(1);
        row.Status.ShouldBe(IntegrationOutboxStatus.Pending);
        row.TargetPath.ShouldBe(CaseTrackerEndpoints.Intake);
        row.AppointmentId.ShouldBe(AppointmentId);
    }

    [Fact]
    public async Task EnqueueAsync_WithTheSameKeyTwice_CollapsesToOneRow()
    {
        // A redelivered approval event must not push the same case twice.
        var (manager, rows) = Build();

        var first = await EnqueueAsync(manager, "same-key");
        var second = await EnqueueAsync(manager, "same-key");

        rows.Count.ShouldBe(1);
        second.Id.ShouldBe(first.Id);
    }

    [Fact]
    public async Task EnqueueAsync_WithADifferentKey_InsertsASecondRow()
    {
        // A genuinely newer version of the appointment SHOULD be pushed again.
        var (manager, rows) = Build();

        await EnqueueAsync(manager, "key-1");
        await EnqueueAsync(manager, "key-2");

        rows.Count.ShouldBe(2);
    }

    [Fact]
    public async Task ClaimDueBatchAsync_ClaimsDueRows_AndSkipsAlreadyLeasedOnes()
    {
        var (manager, _) = Build();
        await EnqueueAsync(manager, "key-1");

        var first = await manager.ClaimDueBatchAsync(Now, Lease, batchSize: 10);
        first.Count.ShouldBe(1);

        // A second, overlapping drain must not get the same row while the lease holds.
        var second = await manager.ClaimDueBatchAsync(Now.AddSeconds(1), Lease, batchSize: 10);
        second.ShouldBeEmpty();
    }

    [Fact]
    public async Task ClaimDueBatchAsync_RespectsTheBatchSize()
    {
        var (manager, _) = Build();
        await EnqueueAsync(manager, "key-1");
        await EnqueueAsync(manager, "key-2");
        await EnqueueAsync(manager, "key-3");

        var claimed = await manager.ClaimDueBatchAsync(Now, Lease, batchSize: 2);

        claimed.Count.ShouldBe(2);
    }

    [Fact]
    public void BuildIdempotencyKey_IsStableForTheSameInputs_AndDiffersByVersion()
    {
        var a = IntegrationOutboxManager.BuildIdempotencyKey(
            IntegrationMessageType.Intake, AppointmentId, "2026-07-27T12:00:00.0000000Z");
        var same = IntegrationOutboxManager.BuildIdempotencyKey(
            IntegrationMessageType.Intake, AppointmentId, "2026-07-27T12:00:00.0000000Z");
        var newerVersion = IntegrationOutboxManager.BuildIdempotencyKey(
            IntegrationMessageType.Intake, AppointmentId, "2026-07-27T12:00:01.0000000Z");

        a.ShouldBe(same);
        a.ShouldNotBe(newerVersion);
    }

    [Fact]
    public void BuildIdempotencyKey_DiffersByAppointment()
    {
        var a = IntegrationOutboxManager.BuildIdempotencyKey(
            IntegrationMessageType.Intake, AppointmentId, "v1");
        var b = IntegrationOutboxManager.BuildIdempotencyKey(
            IntegrationMessageType.Intake, Guid.NewGuid(), "v1");

        a.ShouldNotBe(b);
    }

    [Fact]
    public void BuildIdempotencyKey_FitsTheColumn()
    {
        var key = IntegrationOutboxManager.BuildIdempotencyKey(
            IntegrationMessageType.Intake, AppointmentId, "2026-07-27T12:00:00.0000000Z");

        key.Length.ShouldBeLessThanOrEqualTo(IntegrationOutboxConsts.IdempotencyKeyMaxLength);
    }
}
