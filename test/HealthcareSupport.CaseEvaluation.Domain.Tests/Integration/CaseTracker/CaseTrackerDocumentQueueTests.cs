using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Integration.CaseTracker.Jobs;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Guids;
using Volo.Abp.Uow;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Unit tests for <see cref="CaseTrackerDocumentQueue"/>: the shared enqueue path every document
/// trigger funnels through. Three behaviours matter and none is visible from the entity tests --
/// the idempotency key must be derived from the entry SET so a replayed accept collapses, the drain
/// enqueue must be deferred until the staff action's transaction commits, and nothing may be written
/// for an appointment whose intake has not been queued yet (#931).
///
/// <para>The intake-exists QUERY itself (every status counts, only Intake rows count) is proven
/// against the real provider in <c>EfCoreIntegrationOutboxRepositoryTests</c>. Here the repository is
/// a substitute, so asserting those rules would only test the stub; these tests prove the queue ASKS,
/// for the right appointment, and obeys the answer.</para>
/// </summary>
public class CaseTrackerDocumentQueueTests
{
    private static readonly Guid TenantId = new("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
    private static readonly Guid AppointmentId = new("8f14e45f-ceea-467a-9f3a-1a2b3c4d5e6f");
    private static readonly Guid OtherAppointmentId = new("0b7d2e91-5c3a-4f86-9e1d-7a4c6b8e2f05");
    private static readonly Guid DocumentId = new("f97796c9-365b-4ad3-a164-08f72981cae3");
    private static readonly Guid OtherDocumentId = new("c3d4e5f6-a7b8-49ca-8bdc-ed2143658709");

    private sealed class Harness
    {
        public CaseTrackerDocumentQueue Queue { get; init; } = null!;
        public IIntegrationOutboxRepository Repository { get; init; } = null!;
        public List<IntegrationOutboxItem> Rows { get; init; } = null!;
        public IBackgroundJobManager Jobs { get; init; } = null!;
        public List<Func<Task>> DeferredCallbacks { get; init; } = null!;

        /// <summary>What the queue wrote: the ledger minus any seeded intake row.</summary>
        public List<IntegrationOutboxItem> DocumentRows =>
            Rows.Where(x => x.MessageType == IntegrationMessageType.DocumentUpdate).ToList();
    }

    private static IntegrationOutboxItem IntakeRow(Guid appointmentId) =>
        new(
            new Guid("5e2a9c71-3d84-4b6f-a0e2-9c1d7f4b8a36"),
            TenantId,
            IntegrationMessageType.Intake,
            CaseTrackerEndpoints.Intake,
            appointmentId,
            "{\"data\":{}}",
            "intake-key-" + appointmentId.ToString("N"));

    /// <param name="withAmbientUow">
    /// True models a staff action (an ambient unit of work exists, so the drain enqueue must be
    /// deferred); false models a caller with no UoW, where enqueueing directly is correct.
    /// </param>
    /// <param name="withIntake">
    /// Whether <see cref="AppointmentId"/> already has an intake row. True for every test about what
    /// the queue WRITES; false only for the tests about the #931 gate itself.
    /// </param>
    private static Harness Build(bool withAmbientUow, bool withIntake = true)
    {
        var rows = new List<IntegrationOutboxItem>();

        // LOAD-BEARING, not setup noise. The queue writes nothing for an appointment with no intake
        // row (#931). Without this seed the gate would suppress every write: the positive tests below
        // would fail for the wrong reason, and the empty-entries test would pass for the wrong one.
        if (withIntake)
        {
            rows.Add(IntakeRow(AppointmentId));
        }

        var repo = Substitute.For<IIntegrationOutboxRepository>();
        repo.GetQueryableAsync().Returns(_ => rows.AsQueryable());
        // Mirrors EfCoreIntegrationOutboxRepository.GetForAppointmentAsync: same appointment and type,
        // NEWEST FIRST. The list is in insertion order, so reversing it gives newest first.
        repo.GetForAppointmentAsync(Arg.Any<Guid>(), Arg.Any<IntegrationMessageType>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(rows
                .Where(r => r.AppointmentId == ci.ArgAt<Guid>(0) && r.MessageType == ci.ArgAt<IntegrationMessageType>(1))
                .Reverse()
                .ToList()));
        repo.HasIntakeAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(rows.Any(x =>
                x.AppointmentId == ci.ArgAt<Guid>(0) && x.MessageType == IntegrationMessageType.Intake)));
        repo.InsertAsync(Arg.Any<IntegrationOutboxItem>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var item = ci.Arg<IntegrationOutboxItem>();
                rows.Add(item);
                return Task.FromResult(item);
            });

        var deferred = new List<Func<Task>>();
        var uowManager = Substitute.For<IUnitOfWorkManager>();
        if (withAmbientUow)
        {
            var uow = Substitute.For<IUnitOfWork>();
            uow.When(x => x.OnCompleted(Arg.Any<Func<Task>>()))
                .Do(ci => deferred.Add(ci.Arg<Func<Task>>()));
            uowManager.Current.Returns(uow);
        }
        else
        {
            // MUST be explicit. NSubstitute auto-substitutes interface-returning members, so an
            // unconfigured `Current` hands back a stub IUnitOfWork rather than null -- which would
            // silently route this harness down the deferred branch it is meant to exclude.
            uowManager.Current.Returns((IUnitOfWork?)null);
        }

        var jobs = Substitute.For<IBackgroundJobManager>();

        return new Harness
        {
            Queue = new CaseTrackerDocumentQueue(
                new IntegrationOutboxManager(repo, SimpleGuidGenerator.Instance),
                jobs,
                uowManager,
                NullLogger<CaseTrackerDocumentQueue>.Instance),
            Repository = repo,
            Rows = rows,
            Jobs = jobs,
            DeferredCallbacks = deferred,
        };
    }

    private static IntakeDocumentEntry Entry(Guid id, string updatedAt) => new()
    {
        Id = id,
        Source = DocumentEntryMapper.DocumentSource,
        DocumentName = "Medical Records",
        FileName = "records.pdf",
        ContentType = "application/pdf",
        Status = "Accepted",
        ObjectKey = "tenants/b8844bba-414c-e238-4a71-3a22841f21af/records",
        CreatedAtUtc = "2026-07-28T10:00:00.0000000Z",
        UpdatedAt = updatedAt,
    };

    [Fact]
    public async Task EnqueueDocumentEntriesAsync_WritesOneRowTargetedAtTheAppointment()
    {
        var h = Build(withAmbientUow: false);

        var row = await h.Queue.EnqueueDocumentEntriesAsync(
            AppointmentId, TenantId, new[] { Entry(DocumentId, "2026-07-28T11:00:00.0000000Z") });

        h.DocumentRows.Count.ShouldBe(1);
        row.ShouldNotBeNull();
        row!.MessageType.ShouldBe(IntegrationMessageType.DocumentUpdate);
        row.TargetPath.ShouldBe(CaseTrackerEndpoints.DocumentUpdate(AppointmentId));
        row.AppointmentId.ShouldBe(AppointmentId);
        row.Payload.TrimStart()[0].ShouldBe('[');
    }

    [Fact]
    public async Task EnqueueDocumentEntriesAsync_WithTheSameEntrySetTwice_CollapsesToOneRow()
    {
        // A redelivered accept event must not push the same document twice.
        var h = Build(withAmbientUow: false);
        var entries = new[] { Entry(DocumentId, "2026-07-28T11:00:00.0000000Z") };

        var first = await h.Queue.EnqueueDocumentEntriesAsync(AppointmentId, TenantId, entries);
        var second = await h.Queue.EnqueueDocumentEntriesAsync(AppointmentId, TenantId, entries);

        h.DocumentRows.Count.ShouldBe(1);
        second.ShouldNotBeNull();
        second!.Id.ShouldBe(first!.Id);
    }

    [Fact]
    public async Task EnqueueDocumentEntriesAsync_WhenTheDocumentChanges_WritesASecondRow()
    {
        var h = Build(withAmbientUow: false);

        await h.Queue.EnqueueDocumentEntriesAsync(
            AppointmentId, TenantId, new[] { Entry(DocumentId, "2026-07-28T11:00:00.0000000Z") });
        await h.Queue.EnqueueDocumentEntriesAsync(
            AppointmentId, TenantId, new[] { Entry(DocumentId, "2026-07-28T12:00:00.0000000Z") });

        h.DocumentRows.Count.ShouldBe(2);
    }

    [Fact]
    public async Task EnqueueDocumentEntriesAsync_KeyIgnoresEntryOrder()
    {
        // The array is a SET of documents; listing the same two in the other order is the same
        // message and must not produce a duplicate push.
        var h = Build(withAmbientUow: false);
        var a = Entry(DocumentId, "2026-07-28T11:00:00.0000000Z");
        var b = Entry(OtherDocumentId, "2026-07-28T11:05:00.0000000Z");

        await h.Queue.EnqueueDocumentEntriesAsync(AppointmentId, TenantId, new[] { a, b });
        await h.Queue.EnqueueDocumentEntriesAsync(AppointmentId, TenantId, new[] { b, a });

        h.DocumentRows.Count.ShouldBe(1);
    }

    [Fact]
    public async Task EnqueueDeletionsAsync_WritesADeletionPayload()
    {
        var h = Build(withAmbientUow: false);

        var row = await h.Queue.EnqueueDeletionsAsync(
            AppointmentId,
            TenantId,
            new[] { new DocumentDeletionEntry { Id = DocumentId, UpdatedAt = "2026-07-28T12:00:00.0000000Z" } });

        row.ShouldNotBeNull();
        row!.MessageType.ShouldBe(IntegrationMessageType.DocumentUpdate);
        row.Payload.ShouldContain("\"deleted\":true");
        row.Payload.ShouldNotContain("objectKey");
    }

    [Fact]
    public async Task EnqueueDeletionsAsync_DoesNotCollideWithAnEntryForTheSameDocument()
    {
        // Accepting then rejecting the same document at the same instant are DIFFERENT messages;
        // if their keys collided the rejection would be silently dropped.
        var h = Build(withAmbientUow: false);
        const string stamp = "2026-07-28T12:00:00.0000000Z";

        await h.Queue.EnqueueDocumentEntriesAsync(AppointmentId, TenantId, new[] { Entry(DocumentId, stamp) });
        await h.Queue.EnqueueDeletionsAsync(
            AppointmentId, TenantId, new[] { new DocumentDeletionEntry { Id = DocumentId, UpdatedAt = stamp } });

        h.DocumentRows.Count.ShouldBe(2);
    }

    [Fact]
    public async Task EnqueueDocumentEntriesAsync_WithNoEntries_WritesNothing()
    {
        // Guard: an empty array would tell the receiver the appointment has no documents at all.
        var h = Build(withAmbientUow: false);

        var row = await h.Queue.EnqueueDocumentEntriesAsync(
            AppointmentId, TenantId, Array.Empty<IntakeDocumentEntry>());

        row.ShouldBeNull();
        h.DocumentRows.ShouldBeEmpty();
    }

    [Fact]
    public async Task EnqueueDocumentEntriesAsync_WithAnAmbientUow_DefersTheDrainUntilCommit()
    {
        var h = Build(withAmbientUow: true);

        await h.Queue.EnqueueDocumentEntriesAsync(
            AppointmentId, TenantId, new[] { Entry(DocumentId, "2026-07-28T11:00:00.0000000Z") });

        // Enqueueing inline would let a worker query for the row before the staff action committed.
        await h.Jobs.DidNotReceive().EnqueueAsync(
            Arg.Any<IntegrationOutboxDrainArgs>(), Arg.Any<BackgroundJobPriority>(), Arg.Any<TimeSpan?>());
        h.DeferredCallbacks.Count.ShouldBe(1);

        await h.DeferredCallbacks[0]();

        await h.Jobs.Received(1).EnqueueAsync(
            Arg.Any<IntegrationOutboxDrainArgs>(), Arg.Any<BackgroundJobPriority>(), Arg.Any<TimeSpan?>());
    }

    [Fact]
    public async Task EnqueueDocumentEntriesAsync_WithNoUow_EnqueuesTheDrainDirectly()
    {
        var h = Build(withAmbientUow: false);

        await h.Queue.EnqueueDocumentEntriesAsync(
            AppointmentId, TenantId, new[] { Entry(DocumentId, "2026-07-28T11:00:00.0000000Z") });

        await h.Jobs.Received(1).EnqueueAsync(
            Arg.Any<IntegrationOutboxDrainArgs>(), Arg.Any<BackgroundJobPriority>(), Arg.Any<TimeSpan?>());
    }

    [Fact]
    public async Task EnqueueDocumentEntriesAsync_WithNoIntakeRow_WritesNothingAndSchedulesNoDrain()
    {
        // #931: a document accepted between approval and packet settle. Writing it would put the
        // update AHEAD of its own intake in the stream.
        var h = Build(withAmbientUow: true, withIntake: false);

        var row = await h.Queue.EnqueueDocumentEntriesAsync(
            AppointmentId, TenantId, new[] { Entry(DocumentId, "2026-07-28T11:00:00.0000000Z") });

        row.ShouldBeNull();
        h.Rows.ShouldBeEmpty();
        h.DeferredCallbacks.ShouldBeEmpty();
        await h.Jobs.DidNotReceive().EnqueueAsync(
            Arg.Any<IntegrationOutboxDrainArgs>(), Arg.Any<BackgroundJobPriority>(), Arg.Any<TimeSpan?>());
    }

    [Fact]
    public async Task EnqueueDeletionsAsync_WithNoIntakeRow_WritesNothing()
    {
        // Its own test rather than trusting the shared path: the removal handler's remarks say it
        // deliberately skips per-document publication checks, which makes this the path a later
        // reader is most likely to assume is ungated.
        var h = Build(withAmbientUow: false, withIntake: false);

        var row = await h.Queue.EnqueueDeletionsAsync(
            AppointmentId,
            TenantId,
            new[] { new DocumentDeletionEntry { Id = DocumentId, UpdatedAt = "2026-07-28T12:00:00.0000000Z" } });

        row.ShouldBeNull();
        h.Rows.ShouldBeEmpty();
        await h.Jobs.DidNotReceive().EnqueueAsync(
            Arg.Any<IntegrationOutboxDrainArgs>(), Arg.Any<BackgroundJobPriority>(), Arg.Any<TimeSpan?>());
    }

    [Fact]
    public async Task EnqueueDocumentEntriesAsync_WithAnIntakeForAnotherAppointmentOnly_WritesNothing()
    {
        // The gate must ask about THIS appointment. An office-wide "any intake exists" would pass
        // here and let the early update through.
        var h = Build(withAmbientUow: false, withIntake: false);
        h.Rows.Add(IntakeRow(OtherAppointmentId));

        var row = await h.Queue.EnqueueDocumentEntriesAsync(
            AppointmentId, TenantId, new[] { Entry(DocumentId, "2026-07-28T11:00:00.0000000Z") });

        row.ShouldBeNull();
        h.DocumentRows.ShouldBeEmpty();
    }

    [Fact]
    public async Task EnqueueDocumentEntriesAsync_PassesTheCallersCancellationTokenToTheGate()
    {
        // Both public methods took a token and dropped it before #931; the gate is the first I/O on
        // this path that can honour one.
        var h = Build(withAmbientUow: false);
        using var cts = new CancellationTokenSource();

        await h.Queue.EnqueueDocumentEntriesAsync(
            AppointmentId, TenantId, new[] { Entry(DocumentId, "2026-07-28T11:00:00.0000000Z") }, cts.Token);

        await h.Repository.Received(1).HasIntakeAsync(AppointmentId, cts.Token);
    }

    [Fact]
    public async Task EnqueueDocumentEntriesAsync_TakesTheAppointmentLockBeforeCheckingForAnIntake()
    {
        // The review of #1020 found the gate alone loses a document accepted while an intake is
        // being built: the intake reads the document list, then writes its row. The per-appointment
        // lock closes that only if it is taken BEFORE the check; taken after, the check can still
        // read "no intake" while the intake build is in flight. The intake queue's own test pins
        // the other half (lock before the build).
        var h = Build(withAmbientUow: false);
        using var cts = new CancellationTokenSource();

        await h.Queue.EnqueueDocumentEntriesAsync(
            AppointmentId, TenantId, new[] { Entry(DocumentId, "2026-07-28T11:00:00.0000000Z") }, cts.Token);

        Received.InOrder(() =>
        {
            h.Repository.AcquireAppointmentLockAsync(AppointmentId, cts.Token);
            h.Repository.HasIntakeAsync(AppointmentId, cts.Token);
        });
    }

    [Fact]
    public async Task EnqueueDocumentEntriesAsync_WithNoEntries_TakesNoLock()
    {
        // Nothing to write means nothing to order; the empty-payload guard returns before the lock,
        // so a no-op trigger does not queue behind an intake build.
        var h = Build(withAmbientUow: false);

        await h.Queue.EnqueueDocumentEntriesAsync(AppointmentId, TenantId, Array.Empty<IntakeDocumentEntry>());

        await h.Repository.DidNotReceiveWithAnyArgs().AcquireAppointmentLockAsync(default, default);
    }
}
