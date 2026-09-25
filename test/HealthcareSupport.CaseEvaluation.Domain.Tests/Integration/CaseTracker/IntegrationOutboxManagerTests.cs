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
/// redelivered approval event from pushing the same case twice, and the one-row-at-a-time claim (#917).
/// </summary>
public class IntegrationOutboxManagerTests
{
    private static readonly Guid TenantId = new("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
    private static readonly Guid AppointmentId = new("8f14e45f-ceea-467a-9f3a-1a2b3c4d5e6f");
    private static readonly DateTime Now = new(2026, 7, 27, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Lease = TimeSpan.FromSeconds(IntegrationOutboxConsts.LeaseDurationSeconds);

    private static (IntegrationOutboxManager Manager, List<IntegrationOutboxItem> Rows) Build()
    {
        var (manager, rows, _) = BuildWithRepository();
        return (manager, rows);
    }

    private static (IntegrationOutboxManager Manager, List<IntegrationOutboxItem> Rows, IIntegrationOutboxRepository Repository)
        BuildWithRepository()
    {
        var rows = new List<IntegrationOutboxItem>();
        var repo = Substitute.For<IIntegrationOutboxRepository>();
        repo.GetQueryableAsync().Returns(_ => rows.AsQueryable());
        // Mirrors EfCoreIntegrationOutboxRepository.GetForAppointmentAsync: same appointment and type,
        // NEWEST FIRST. The list is in insertion order, so reversing it gives newest first.
        repo.GetForAppointmentAsync(Arg.Any<Guid>(), Arg.Any<IntegrationMessageType>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(rows
                .Where(r => r.AppointmentId == ci.ArgAt<Guid>(0) && r.MessageType == ci.ArgAt<IntegrationMessageType>(1))
                .Reverse()
                .ToList()));
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
        // Mirrors EfCoreIntegrationOutboxRepository.GetDueIdsAsync: the lease gate, oldest first.
        repo.GetDueIdsAsync(Arg.Any<DateTime>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var now = ci.ArgAt<DateTime>(0);
                return Task.FromResult(rows
                    .Where(r => r.Status == IntegrationOutboxStatus.Pending
                        && (r.LockedUntil == null || r.LockedUntil <= now)
                        && (r.NextAttemptAt == null || r.NextAttemptAt <= now))
                    .OrderBy(r => r.CreationTime)
                    .ThenBy(r => r.Id)
                    .Take(ci.ArgAt<int>(1))
                    .Select(r => r.Id)
                    .ToList());
            });

        return (new IntegrationOutboxManager(repo, SimpleGuidGenerator.Instance), rows, repo);
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
    public async Task ClaimNextDueAsync_LeasesADueRow_AndAnOverlappingDrainDoesNotGetIt()
    {
        var (manager, _) = Build();
        var row = await EnqueueAsync(manager, "key-1");

        var first = await manager.ClaimNextDueAsync(Now, Lease);
        first.ShouldNotBeNull();
        first.Id.ShouldBe(row.Id);
        first.LockedUntil.ShouldBe(Now.Add(Lease));

        // A second, overlapping drain must not get the same row while the lease holds.
        (await manager.ClaimNextDueAsync(Now.AddSeconds(1), Lease)).ShouldBeNull();
    }

    [Fact]
    public async Task ClaimNextDueAsync_ClaimsOneRowPerCall()
    {
        // #917: one row at a time, so each claim can commit before that row's HTTP call.
        var (manager, rows) = Build();
        await EnqueueAsync(manager, "key-1");
        await EnqueueAsync(manager, "key-2");

        await manager.ClaimNextDueAsync(Now, Lease);

        rows.Count(r => r.LockedUntil != null).ShouldBe(1);
    }

    [Fact]
    public async Task ClaimNextDueAsync_WhenAnotherDrainWinsTheFirstCandidate_TakesTheNext()
    {
        // The candidate list is read before the lease; another drain can take a row in between. That
        // row's lease then fails, and the claim must move on rather than report nothing due.
        var (manager, rows, repo) = BuildWithRepository();
        await EnqueueAsync(manager, "key-1");
        await EnqueueAsync(manager, "key-2");
        var due = await repo.GetDueIdsAsync(Now, 5);
        var lost = due[0];
        repo.TryLeaseAsync(lost, Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));

        var claimed = await manager.ClaimNextDueAsync(Now, Lease);

        claimed.ShouldNotBeNull();
        claimed.Id.ShouldBe(due[1]);
        rows.Single(r => r.Id == lost).LockedUntil.ShouldBeNull();
    }

    [Fact]
    public async Task ClaimNextDueAsync_WhenNothingIsDue_ReturnsNull()
    {
        var (manager, _) = Build();
        var row = await EnqueueAsync(manager, "key-1");
        row.MarkFailed(Now, "503"); // waiting 5 minutes

        (await manager.ClaimNextDueAsync(Now.AddMinutes(1), Lease)).ShouldBeNull();
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

    // ---- #915: when same content collapses, and onto which row ----

    private static string ContentKey(string version) =>
        IntegrationOutboxManager.BuildIdempotencyKey(IntegrationMessageType.Intake, AppointmentId, version);

    private static string DocumentKey(string version) =>
        IntegrationOutboxManager.BuildIdempotencyKey(IntegrationMessageType.DocumentUpdate, AppointmentId, version);

    private static Task<IntegrationOutboxItem> EnqueueDocumentAsync(IntegrationOutboxManager manager, string key) =>
        manager.EnqueueAsync(
            TenantId,
            IntegrationMessageType.DocumentUpdate,
            CaseTrackerEndpoints.DocumentUpdate(AppointmentId),
            AppointmentId,
            "[]",
            key);

    /// <summary>Moves a row to the given status the way the drain and the dead-letter screen do.</summary>
    private static void Settle(IntegrationOutboxItem row, IntegrationOutboxStatus status)
    {
        switch (status)
        {
            case IntegrationOutboxStatus.Sent:
                row.MarkSent(Now);
                break;
            case IntegrationOutboxStatus.Failed:
                row.MarkFatal(Now, "401 invalid token");
                break;
            case IntegrationOutboxStatus.Resolved:
                row.MarkFatal(Now, "401 invalid token");
                row.MarkResolved(Now);
                break;
        }

        // Guards the fixture: MarkResolved is a no-op on anything but a Failed row.
        row.Status.ShouldBe(status);
    }

    [Fact]
    public async Task EnqueueAsync_IntakeRevertedAToBToA_QueuesAThirdRow()
    {
        // #915's required regression test (agreed with Levon 2026-09-16). A patient field changed
        // and changed back: the third payload is byte-identical to the first, whose row was already
        // Sent, so the old key-only lookup returned it and the Case Tracker kept B for good.
        var (manager, rows) = Build();

        var a = await EnqueueAsync(manager, ContentKey("A"));
        Settle(a, IntegrationOutboxStatus.Sent);
        var b = await EnqueueAsync(manager, ContentKey("B"));
        Settle(b, IntegrationOutboxStatus.Sent);

        var backToA = await EnqueueAsync(manager, ContentKey("A"));

        rows.Count.ShouldBe(3);
        backToA.Id.ShouldNotBe(a.Id);
        backToA.Status.ShouldBe(IntegrationOutboxStatus.Pending);
        backToA.IdempotencyKey.ShouldNotBe(a.IdempotencyKey); // the unique index would refuse a repeat
    }

    [Theory]
    [InlineData(IntegrationOutboxStatus.Pending)]
    [InlineData(IntegrationOutboxStatus.Sent)]
    public async Task EnqueueAsync_IntakeSameContentAsANewestRowInHandOrDelivered_Collapses(IntegrationOutboxStatus status)
    {
        // The replay protection the key exists for: a redelivered event for the current state.
        var (manager, rows) = Build();
        var first = await EnqueueAsync(manager, ContentKey("A"));
        if (status != IntegrationOutboxStatus.Pending)
        {
            Settle(first, status);
        }

        var replay = await EnqueueAsync(manager, ContentKey("A"));

        rows.Count.ShouldBe(1);
        replay.Id.ShouldBe(first.Id);
    }

    [Theory]
    [InlineData(IntegrationOutboxStatus.Failed)]
    [InlineData(IntegrationOutboxStatus.Resolved)]
    public async Task EnqueueAsync_IntakeSameContentAsANewestRowThatNeverArrived_InsertsAFreshRow(IntegrationOutboxStatus status)
    {
        // #961's root: a manual retry of an unchanged dead letter rebuilt the same content, collapsed
        // onto the dead letter itself, and nothing was ever sent. Failed never arrived; Resolved was
        // set aside by a person. Either way a new enqueue is a genuine new attempt.
        var (manager, rows) = Build();
        var first = await EnqueueAsync(manager, ContentKey("A"));
        Settle(first, status);

        var retry = await EnqueueAsync(manager, ContentKey("A"));

        rows.Count.ShouldBe(2);
        retry.Id.ShouldNotBe(first.Id);
        retry.Status.ShouldBe(IntegrationOutboxStatus.Pending);
        retry.IdempotencyKey.ShouldNotBe(first.IdempotencyKey);
    }

    [Fact]
    public async Task EnqueueAsync_IntakeReplayedAgainstAPersistentlyFailingEndpoint_AddsARowPerAttempt()
    {
        // The side effect #915 records rather than hides: not collapsing onto Failed changes the
        // AUTOMATIC path too. The volume guard bounds delivery, not the row count.
        var (manager, rows) = Build();

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var row = await EnqueueAsync(manager, ContentKey("A"));
            Settle(row, IntegrationOutboxStatus.Failed);
        }

        rows.Count.ShouldBe(3);
        rows.Select(r => r.IdempotencyKey).Distinct().Count().ShouldBe(3);
    }

    [Fact]
    public async Task EnqueueAsync_IntakeRepeatedReverts_EachGetsADistinctKey()
    {
        // A -> B -> A -> B -> A: every content repeat needs its own generation key.
        var (manager, rows) = Build();

        foreach (var version in new[] { "A", "B", "A", "B", "A" })
        {
            var row = await EnqueueAsync(manager, ContentKey(version));
            Settle(row, IntegrationOutboxStatus.Sent);
        }

        rows.Count.ShouldBe(5);
        rows.Select(r => r.IdempotencyKey).Distinct().Count().ShouldBe(5);
        rows.ShouldAllBe(r => r.IdempotencyKey.Length <= IntegrationOutboxConsts.IdempotencyKeyMaxLength);
    }

    [Fact]
    public async Task EnqueueAsync_IntakeReplayAfterARevert_CollapsesOntoTheNewestRow()
    {
        // After A -> B -> A, a replay of A must collapse onto the THIRD row (a generation key), which
        // is only possible if the content match understands generation keys.
        var (manager, rows) = Build();
        foreach (var version in new[] { "A", "B" })
        {
            Settle(await EnqueueAsync(manager, ContentKey(version)), IntegrationOutboxStatus.Sent);
        }

        var third = await EnqueueAsync(manager, ContentKey("A"));
        var replay = await EnqueueAsync(manager, ContentKey("A"));

        rows.Count.ShouldBe(3);
        replay.Id.ShouldBe(third.Id);
    }

    [Fact]
    public async Task EnqueueAsync_DocumentReplayForAnOlderDocument_CollapsesEvenAfterANewerOne()
    {
        // Decided 2026-09-23: document updates are deltas, not snapshots. A replayed accept for
        // document 1 after document 2 went out must NOT re-send document 1, so documents are compared
        // with ANY earlier row, not only the newest.
        var (manager, rows) = Build();
        var doc1 = await EnqueueDocumentAsync(manager, DocumentKey("doc1@t1"));
        Settle(doc1, IntegrationOutboxStatus.Sent);
        Settle(await EnqueueDocumentAsync(manager, DocumentKey("doc2@t1")), IntegrationOutboxStatus.Sent);

        var replay = await EnqueueDocumentAsync(manager, DocumentKey("doc1@t1"));

        rows.Count.ShouldBe(2);
        replay.Id.ShouldBe(doc1.Id);
    }

    [Fact]
    public async Task EnqueueAsync_DocumentSameContentAsAFailedRow_InsertsAFreshRow()
    {
        // The status test applies to documents too: a dead-lettered update never arrived.
        var (manager, rows) = Build();
        var failed = await EnqueueDocumentAsync(manager, DocumentKey("doc1@t1"));
        Settle(failed, IntegrationOutboxStatus.Failed);

        var again = await EnqueueDocumentAsync(manager, DocumentKey("doc1@t1"));

        rows.Count.ShouldBe(2);
        again.Id.ShouldNotBe(failed.Id);
        again.Status.ShouldBe(IntegrationOutboxStatus.Pending);
    }

    [Fact]
    public async Task EnqueueAsync_TheIntakeRuleIgnoresDocumentRowsAndViceVersa()
    {
        // The newest row is per message type: a document update after the intake must not stop a
        // replayed intake from recognising the intake row as the newest intake.
        var (manager, rows) = Build();
        var intake = await EnqueueAsync(manager, ContentKey("A"));
        Settle(intake, IntegrationOutboxStatus.Sent);
        await EnqueueDocumentAsync(manager, DocumentKey("doc1@t1"));

        var replay = await EnqueueAsync(manager, ContentKey("A"));

        rows.Count.ShouldBe(2);
        replay.Id.ShouldBe(intake.Id);
    }

    [Fact]
    public void BuildGenerationKey_IsStableDistinctAndFitsTheColumn()
    {
        var content = ContentKey("A");

        var first = IntegrationOutboxManager.BuildGenerationKey(content, 1);

        first.ShouldBe(IntegrationOutboxManager.BuildGenerationKey(content, 1));
        first.ShouldNotBe(content);
        first.ShouldNotBe(IntegrationOutboxManager.BuildGenerationKey(content, 2));
        first.Length.ShouldBeLessThanOrEqualTo(IntegrationOutboxConsts.IdempotencyKeyMaxLength);
    }
}
