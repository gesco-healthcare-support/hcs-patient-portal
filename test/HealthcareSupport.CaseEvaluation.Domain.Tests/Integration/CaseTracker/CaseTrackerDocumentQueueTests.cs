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
/// trigger funnels through. Two behaviours matter and neither is visible from the entity tests --
/// the idempotency key must be derived from the entry SET so a replayed accept collapses, and the
/// drain enqueue must be deferred until the staff action's transaction commits.
/// </summary>
public class CaseTrackerDocumentQueueTests
{
    private static readonly Guid TenantId = new("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
    private static readonly Guid AppointmentId = new("8f14e45f-ceea-467a-9f3a-1a2b3c4d5e6f");
    private static readonly Guid DocumentId = new("f97796c9-365b-4ad3-a164-08f72981cae3");
    private static readonly Guid OtherDocumentId = new("c3d4e5f6-a7b8-49ca-8bdc-ed2143658709");

    private sealed class Harness
    {
        public CaseTrackerDocumentQueue Queue { get; init; } = null!;
        public List<IntegrationOutboxItem> Rows { get; init; } = null!;
        public IBackgroundJobManager Jobs { get; init; } = null!;
        public List<Func<Task>> DeferredCallbacks { get; init; } = null!;
    }

    /// <param name="withAmbientUow">
    /// True models a staff action (an ambient unit of work exists, so the drain enqueue must be
    /// deferred); false models a caller with no UoW, where enqueueing directly is correct.
    /// </param>
    private static Harness Build(bool withAmbientUow)
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

        h.Rows.Count.ShouldBe(1);
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

        h.Rows.Count.ShouldBe(1);
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

        h.Rows.Count.ShouldBe(2);
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

        h.Rows.Count.ShouldBe(1);
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

        h.Rows.Count.ShouldBe(2);
    }

    [Fact]
    public async Task EnqueueDocumentEntriesAsync_WithNoEntries_WritesNothing()
    {
        // Guard: an empty array would tell the receiver the appointment has no documents at all.
        var h = Build(withAmbientUow: false);

        var row = await h.Queue.EnqueueDocumentEntriesAsync(
            AppointmentId, TenantId, Array.Empty<IntakeDocumentEntry>());

        row.ShouldBeNull();
        h.Rows.ShouldBeEmpty();
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
}
