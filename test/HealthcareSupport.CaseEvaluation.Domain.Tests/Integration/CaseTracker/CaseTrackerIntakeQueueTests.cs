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
/// Unit tests for <see cref="CaseTrackerIntakeQueue"/>, which turns "push this appointment" into an
/// outbox row plus a drain job.
///
/// <para><b>The #931 ordering guarantee.</b> The intake builds its payload from the current document
/// list and only then writes its row. A document accepted in that gap would be suppressed by the
/// document gate (no intake row yet) and be missing from the payload. The per-appointment lock closes
/// the gap only if the intake takes it BEFORE it reads; <c>CaseTrackerDocumentQueueTests</c> pins the
/// other half. The lock itself is a SQL Server application lock, a no-op on the SQLite test database,
/// so these tests prove ORDER, not blocking.</para>
///
/// <para><b>The row and the drain.</b> Queueing the SAME appointment state twice collapses to one row --
/// a replayed event cannot push a duplicate case. The drain is enqueued at once when there is no unit
/// of work, and only AFTER COMMIT when there is one, so a worker can never race a row that is not saved
/// yet. A drain enqueue that fails because the scope was already disposed is swallowed (the row is
/// committed and the sweep re-drives it); any other failure is not.</para>
///
/// <para>The outbox manager is real over a substituted repository backed by a list; the payload
/// builder, job manager and unit of work are substitutes. Nothing is sent. All fixture data is
/// synthetic.</para>
/// </summary>
public class CaseTrackerIntakeQueueTests
{
    private static readonly Guid TenantId = new("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
    private static readonly Guid AppointmentId = new("3c9e1f47-8a2d-4b6e-9f05-7d1a2c4e6b83");

    private sealed class Harness
    {
        public CaseTrackerIntakeQueue Queue { get; init; } = null!;
        public IIntegrationOutboxRepository Repository { get; init; } = null!;
        public IIntakePayloadBuilder PayloadBuilder { get; init; } = null!;
        public IBackgroundJobManager Jobs { get; init; } = null!;
        public IUnitOfWorkManager UnitOfWorkManager { get; init; } = null!;
        public List<IntegrationOutboxItem> Rows { get; init; } = null!;

        public List<IntegrationOutboxDrainArgs> DrainJobs() =>
            Jobs.ReceivedCalls().SelectMany(c => c.GetArguments()).OfType<IntegrationOutboxDrainArgs>().ToList();

        /// <summary>
        /// Makes the queue run inside a unit of work and returns a getter for the after-commit callback
        /// the queue registers on it (null until it registers one).
        /// </summary>
        public Func<Func<Task>?> RunInsideAUnitOfWork()
        {
            var uow = Substitute.For<IUnitOfWork>();
            Func<Task>? onCompleted = null;
            uow.When(u => u.OnCompleted(Arg.Any<Func<Task>>())).Do(ci => onCompleted = ci.Arg<Func<Task>>());
            UnitOfWorkManager.Current.Returns(uow);
            return () => onCompleted;
        }
    }

    private static IntakeEnvelope Envelope() => new()
    {
        Data = new IntakePayload
        {
            AppointmentId = AppointmentId,
            ConfirmationNumber = "A00123",
            UpdatedAt = "2026-09-01T00:00:00Z",
            Patient = new IntakePatientSection
            {
                FirstName = "Testadora",
                LastName = "Synthetica",
                Street = "1200 Sample Street",
                City = "Sample City",
                ZipCode = "90210",
            },
        },
        Meta = new IntakeMeta
        {
            RequestId = new Guid("7e2b4c9a-1d3f-4a58-b6c0-2e9f8a7d5c41"),
            Timestamp = "2026-09-01T00:00:00.0000000Z",
        },
    };

    private static Harness Build()
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

        var payloadBuilder = Substitute.For<IIntakePayloadBuilder>();
        payloadBuilder.BuildAsync(AppointmentId, Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(Envelope()));

        // MUST be explicit: NSubstitute would otherwise hand back a stub IUnitOfWork for Current.
        var uowManager = Substitute.For<IUnitOfWorkManager>();
        uowManager.Current.Returns((IUnitOfWork?)null);

        var jobs = Substitute.For<IBackgroundJobManager>();

        return new Harness
        {
            Queue = new CaseTrackerIntakeQueue(
                payloadBuilder,
                new IntegrationOutboxManager(repo, SimpleGuidGenerator.Instance),
                jobs,
                uowManager,
                NullLogger<CaseTrackerIntakeQueue>.Instance),
            Repository = repo,
            PayloadBuilder = payloadBuilder,
            Jobs = jobs,
            UnitOfWorkManager = uowManager,
            Rows = rows,
        };
    }

    [Fact]
    public async Task EnqueueIntakeAsync_TakesTheAppointmentLockBeforeBuildingThePayload()
    {
        var h = Build();
        using var cts = new CancellationTokenSource();

        await h.Queue.EnqueueIntakeAsync(AppointmentId, TenantId, cts.Token);

        // Lock, THEN read the document list (inside the build), THEN write the row. Any other order
        // leaves a gap in which an accepted document is both suppressed and missing from the intake.
        Received.InOrder(() =>
        {
            h.Repository.AcquireAppointmentLockAsync(AppointmentId, cts.Token);
            h.PayloadBuilder.BuildAsync(AppointmentId, cts.Token);
            h.Repository.InsertAsync(Arg.Any<IntegrationOutboxItem>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task EnqueueIntakeAsync_StillWritesTheIntakeRow()
    {
        // The lock must not change what is written: one Intake row targeted at the intake endpoint.
        var h = Build();

        var row = await h.Queue.EnqueueIntakeAsync(AppointmentId, TenantId);

        h.Rows.Count.ShouldBe(1);
        row.MessageType.ShouldBe(IntegrationMessageType.Intake);
        row.TargetPath.ShouldBe(CaseTrackerEndpoints.Intake);
        row.AppointmentId.ShouldBe(AppointmentId);
    }

    [Fact]
    public async Task EnqueueIntakeAsync_WhenTheLockIsNotGranted_WritesNothing()
    {
        // Fail loudly, never carry on unlocked: an unlocked build silently reopens the race.
        var h = Build();
        h.Repository.AcquireAppointmentLockAsync(AppointmentId, Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("lock not granted")));

        await Should.ThrowAsync<InvalidOperationException>(() => h.Queue.EnqueueIntakeAsync(AppointmentId, TenantId));

        h.Rows.ShouldBeEmpty();
        await h.PayloadBuilder.DidNotReceiveWithAnyArgs().BuildAsync(default, default);
    }

    [Fact]
    public async Task EnqueueIntakeAsync_WithNoUnitOfWork_WritesAPendingRowForTheOffice_AndEnqueuesTheDrainAtOnce()
    {
        var h = Build();

        var row = await h.Queue.EnqueueIntakeAsync(AppointmentId, TenantId);

        h.Rows.ShouldHaveSingleItem().ShouldBeSameAs(row);
        row.TenantId.ShouldBe(TenantId);
        row.Status.ShouldBe(IntegrationOutboxStatus.Pending);
        h.DrainJobs().ShouldHaveSingleItem().TenantId.ShouldBe(TenantId);
    }

    /// <summary>
    /// The same appointment in the same state queues ONE row: a redelivered event returns the existing
    /// row instead of inserting a duplicate case.
    /// </summary>
    [Fact]
    public async Task EnqueueIntakeAsync_TheSameStateTwice_CollapsesToOneRow()
    {
        var h = Build();

        var first = await h.Queue.EnqueueIntakeAsync(AppointmentId, TenantId);
        var second = await h.Queue.EnqueueIntakeAsync(AppointmentId, TenantId);

        second.ShouldBeSameAs(first);
        h.Rows.Count.ShouldBe(1);
    }

    /// <summary>
    /// Inside a unit of work the drain is NOT enqueued at once -- it is registered to run after commit,
    /// so a worker cannot pick up a row that is not saved yet.
    /// </summary>
    [Fact]
    public async Task EnqueueIntakeAsync_InsideAUnitOfWork_EnqueuesTheDrainOnlyAfterCommit()
    {
        var h = Build();
        var onCompleted = h.RunInsideAUnitOfWork();

        await h.Queue.EnqueueIntakeAsync(AppointmentId, TenantId);

        h.DrainJobs().ShouldBeEmpty("before commit, no drain may be queued");
        var afterCommit = onCompleted().ShouldNotBeNull();
        await afterCommit();
        h.DrainJobs().ShouldHaveSingleItem().TenantId.ShouldBe(TenantId);
    }

    /// <summary>
    /// After commit, a drain enqueue that fails because the scope was already disposed is swallowed --
    /// the row is committed and the reconciliation sweep will re-drive it -- so a successful approval
    /// is not failed by it. The Fact after this one is its control: any other failure propagates.
    /// </summary>
    [Fact]
    public async Task EnqueueIntakeAsync_ADisposedScopeAfterCommit_IsSwallowed()
    {
        var h = Build();
        var onCompleted = h.RunInsideAUnitOfWork();
        h.Jobs.EnqueueAsync(Arg.Any<IntegrationOutboxDrainArgs>(), Arg.Any<BackgroundJobPriority>(), Arg.Any<TimeSpan?>())
            .Returns(Task.FromException<string>(new ObjectDisposedException("TEST-scope")));

        await h.Queue.EnqueueIntakeAsync(AppointmentId, TenantId);

        await Should.NotThrowAsync(() => onCompleted()!());
        h.Rows.Count.ShouldBe(1, "the row is committed whatever happens to the drain enqueue");
    }

    [Fact]
    public async Task EnqueueIntakeAsync_AnyOtherFailureAfterCommit_IsNotSwallowed()
    {
        var h = Build();
        var onCompleted = h.RunInsideAUnitOfWork();
        h.Jobs.EnqueueAsync(Arg.Any<IntegrationOutboxDrainArgs>(), Arg.Any<BackgroundJobPriority>(), Arg.Any<TimeSpan?>())
            .Returns(Task.FromException<string>(new InvalidOperationException("TEST-job store down")));

        await h.Queue.EnqueueIntakeAsync(AppointmentId, TenantId);

        var thrown = await Should.ThrowAsync<InvalidOperationException>(() => onCompleted()!());
        thrown.Message.ShouldBe("TEST-job store down");
    }
}
