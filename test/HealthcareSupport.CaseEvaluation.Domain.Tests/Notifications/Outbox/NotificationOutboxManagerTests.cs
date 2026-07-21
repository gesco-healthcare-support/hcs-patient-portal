using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Notifications.Outbox;

/// <summary>
/// T8 unit tests for <see cref="NotificationOutboxManager"/>: the idempotent
/// enqueue (dedup by key -- the atomic approval-UoW write) and the due-batch
/// claim (what the drain job calls). Repository mocked with NSubstitute; the
/// queryable is List-backed so LINQ runs in-memory, matching the existing
/// AppointmentPacketManagerTests style.
/// </summary>
public class NotificationOutboxManagerTests
{
    private static readonly Guid TenantId = new("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
    private static readonly DateTime Now = new(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(5);

    private static NotificationOutboxItem Pending(string key, DateTime? lockedUntil = null, DateTime? nextAttemptAt = null)
    {
        var item = new NotificationOutboxItem(
            Guid.NewGuid(), TenantId,
            to: "party@example.test", cc: null, subject: "s", body: "b",
            isBodyHtml: true, context: "ctx", idempotencyKey: key);
        if (lockedUntil.HasValue)
        {
            item.TryClaim(lockedUntil.Value.Subtract(Lease), Lease); // sets LockedUntil = lockedUntil
        }
        if (nextAttemptAt.HasValue)
        {
            // Push NextAttemptAt into the future by failing once with a matching backoff.
            item.MarkFailed(Now, "seed", nextAttemptAt.Value - Now);
        }
        return item;
    }

    private static (NotificationOutboxManager manager, INotificationOutboxRepository repo)
        Build(List<NotificationOutboxItem> seed)
    {
        var repo = Substitute.For<INotificationOutboxRepository>();
        repo.GetQueryableAsync().Returns(seed.AsQueryable());
        repo.InsertAsync(Arg.Any<NotificationOutboxItem>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(ci.Arg<NotificationOutboxItem>()));
        repo.UpdateAsync(Arg.Any<NotificationOutboxItem>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(ci.Arg<NotificationOutboxItem>()));
        // Emulate the DB-atomic lease over the in-memory seed: the entity's own TryClaim
        // enforces the same gate the SQL UPDATE would, so ClaimDueBatchAsync exercises its
        // real candidate query + reload while only the atomic write is faked.
        repo.TryLeaseAsync(Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var row = seed.FirstOrDefault(r => r.Id == ci.ArgAt<Guid>(0));
                var now = ci.ArgAt<DateTime>(1);
                var leaseUntil = ci.ArgAt<DateTime>(2);
                return Task.FromResult(row != null && row.TryClaim(now, leaseUntil - now));
            });
        repo.GetAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(seed.First(r => r.Id == ci.ArgAt<Guid>(0))));
        return (new NotificationOutboxManager(repo, SimpleGuidGenerator.Instance), repo);
    }

    [Fact]
    public async Task EnqueueAsync_NewKey_InsertsPendingRow()
    {
        var (manager, repo) = Build(new List<NotificationOutboxItem>());

        var result = await manager.EnqueueAsync(
            TenantId, "party@example.test", null, "s", "b", true, "ctx", "key-1");

        result.Status.ShouldBe(NotificationOutboxStatus.Pending);
        result.IdempotencyKey.ShouldBe("key-1");
        await repo.Received(1).InsertAsync(
            Arg.Any<NotificationOutboxItem>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnqueueAsync_DuplicateKey_ReturnsExistingWithoutInsert()
    {
        var existing = Pending("key-1");
        var (manager, repo) = Build(new List<NotificationOutboxItem> { existing });

        var result = await manager.EnqueueAsync(
            TenantId, "party@example.test", null, "s", "b", true, "ctx", "key-1");

        // Idempotent: the same logical send collapses to the existing row, so a
        // replayed approval UoW cannot create a duplicate PHI email.
        result.Id.ShouldBe(existing.Id);
        await repo.DidNotReceive().InsertAsync(
            Arg.Any<NotificationOutboxItem>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClaimDueBatchAsync_LeasesOnlyDuePendingRows()
    {
        var dueNow = Pending("due-now");
        var leased = Pending("leased", lockedUntil: Now.AddMinutes(3));      // active lease -> not due
        var backoff = Pending("backoff", nextAttemptAt: Now.AddMinutes(30)); // waiting on backoff -> not due
        var (manager, _) = Build(new List<NotificationOutboxItem> { dueNow, leased, backoff });

        var claimed = await manager.ClaimDueBatchAsync(Now, Lease, batchSize: 10);

        claimed.Select(x => x.IdempotencyKey).ShouldBe(new[] { "due-now" });
        dueNow.LockedUntil.ShouldBe(Now.Add(Lease));
    }

    [Fact]
    public async Task ClaimDueBatchAsync_RespectsBatchSize()
    {
        var seed = Enumerable.Range(0, 5).Select(i => Pending($"k{i}")).ToList();
        var (manager, _) = Build(seed);

        var claimed = await manager.ClaimDueBatchAsync(Now, Lease, batchSize: 2);

        claimed.Count.ShouldBe(2);
    }
}
