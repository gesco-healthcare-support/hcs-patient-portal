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
/// Unit coverage for <see cref="CaseTrackerIntakeQueue"/>, which turns "push this appointment" into an
/// outbox row plus a drain job.
///
/// <para><b>WHAT IS PINNED.</b> The row is an Intake push to the intake endpoint for that appointment
/// and office. Queueing the SAME appointment state twice collapses to one row -- a replayed event
/// cannot push a duplicate case. The drain is enqueued at once when there is no unit of work, and only
/// AFTER COMMIT when there is one, so a worker can never race a row that is not saved yet. A drain
/// enqueue that fails because the scope was already disposed is swallowed (the row is committed and
/// the sweep re-drives it); any other failure is not.</para>
///
/// <para><b>The results asserted are the rows inserted and the jobs enqueued.</b> The outbox manager is
/// real over a substituted repository backed by a list; the payload builder, job manager and unit of
/// work are substitutes. Nothing is sent. Synthetic data only (HIPAA).</para>
/// </summary>
public class CaseTrackerIntakeQueueTests
{
    private static readonly Guid OfficeId = new("eeeeeeee-0000-0000-0000-00000000000e");

    private sealed class Rig
    {
        public List<IntegrationOutboxItem> Rows { get; } = new();
        public IIntegrationOutboxRepository Repository { get; } = Substitute.For<IIntegrationOutboxRepository>();
        public IBackgroundJobManager Jobs { get; } = Substitute.For<IBackgroundJobManager>();
        public IUnitOfWorkManager UnitOfWorkManager { get; } = Substitute.For<IUnitOfWorkManager>();

        public Rig()
        {
            Repository.GetQueryableAsync().Returns(_ => Rows.AsQueryable());
            Repository.InsertAsync(Arg.Any<IntegrationOutboxItem>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
                .Returns(ci =>
                {
                    var row = ci.Arg<IntegrationOutboxItem>();
                    Rows.Add(row);
                    return Task.FromResult(row);
                });
            UnitOfWorkManager.Current.Returns((IUnitOfWork?)null);
        }

        public CaseTrackerIntakeQueue Build()
        {
            var payloads = Substitute.For<IIntakePayloadBuilder>();
            payloads.BuildAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(_ => new IntakeEnvelope());
            return new CaseTrackerIntakeQueue(
                payloads,
                new IntegrationOutboxManager(Repository, SimpleGuidGenerator.Instance),
                Jobs,
                UnitOfWorkManager,
                NullLogger<CaseTrackerIntakeQueue>.Instance);
        }

        public List<IntegrationOutboxDrainArgs> DrainJobs() =>
            Jobs.ReceivedCalls().SelectMany(c => c.GetArguments()).OfType<IntegrationOutboxDrainArgs>().ToList();
    }

    [Fact]
    public async Task EnqueueIntakeAsync_WithNoUnitOfWork_WritesAnIntakeRowAndEnqueuesTheDrainAtOnce()
    {
        var rig = new Rig();
        var appointmentId = Guid.NewGuid();

        var row = await rig.Build().EnqueueIntakeAsync(appointmentId, OfficeId);

        rig.Rows.ShouldHaveSingleItem().ShouldBeSameAs(row);
        row.MessageType.ShouldBe(IntegrationMessageType.Intake);
        row.TargetPath.ShouldBe(CaseTrackerEndpoints.Intake);
        row.AppointmentId.ShouldBe(appointmentId);
        row.TenantId.ShouldBe(OfficeId);
        row.Status.ShouldBe(IntegrationOutboxStatus.Pending);
        rig.DrainJobs().ShouldHaveSingleItem().TenantId.ShouldBe(OfficeId);
    }

    /// <summary>
    /// The same appointment in the same state queues ONE row: a redelivered event returns the existing
    /// row instead of inserting a duplicate case.
    /// </summary>
    [Fact]
    public async Task EnqueueIntakeAsync_TheSameStateTwice_CollapsesToOneRow()
    {
        var rig = new Rig();
        var queue = rig.Build();
        var appointmentId = Guid.NewGuid();

        var first = await queue.EnqueueIntakeAsync(appointmentId, OfficeId);
        var second = await queue.EnqueueIntakeAsync(appointmentId, OfficeId);

        second.ShouldBeSameAs(first);
        rig.Rows.Count.ShouldBe(1);
    }

    /// <summary>
    /// Inside a unit of work the drain is NOT enqueued at once -- it is registered to run after commit,
    /// so a worker cannot pick up a row that is not saved yet.
    /// </summary>
    [Fact]
    public async Task EnqueueIntakeAsync_InsideAUnitOfWork_EnqueuesTheDrainOnlyAfterCommit()
    {
        var rig = new Rig();
        var uow = Substitute.For<IUnitOfWork>();
        Func<Task>? onCompleted = null;
        uow.When(u => u.OnCompleted(Arg.Any<Func<Task>>())).Do(ci => onCompleted = ci.Arg<Func<Task>>());
        rig.UnitOfWorkManager.Current.Returns(uow);

        await rig.Build().EnqueueIntakeAsync(Guid.NewGuid(), OfficeId);

        rig.DrainJobs().ShouldBeEmpty("before commit, no drain may be queued");
        onCompleted.ShouldNotBeNull();
        await onCompleted();
        rig.DrainJobs().ShouldHaveSingleItem().TenantId.ShouldBe(OfficeId);
    }

    /// <summary>
    /// After commit, a drain enqueue that fails because the scope was already disposed is swallowed --
    /// the row is committed and the reconciliation sweep will re-drive it -- so a successful approval
    /// is not failed by it.
    /// </summary>
    [Fact]
    public async Task EnqueueIntakeAsync_ADisposedScopeAfterCommit_IsSwallowed()
    {
        var rig = new Rig();
        var uow = Substitute.For<IUnitOfWork>();
        Func<Task>? onCompleted = null;
        uow.When(u => u.OnCompleted(Arg.Any<Func<Task>>())).Do(ci => onCompleted = ci.Arg<Func<Task>>());
        rig.UnitOfWorkManager.Current.Returns(uow);
        rig.Jobs.EnqueueAsync(Arg.Any<IntegrationOutboxDrainArgs>(), Arg.Any<BackgroundJobPriority>(), Arg.Any<TimeSpan?>())
            .Returns(Task.FromException<string>(new ObjectDisposedException("TEST-scope")));

        await rig.Build().EnqueueIntakeAsync(Guid.NewGuid(), OfficeId);

        await Should.NotThrowAsync(() => onCompleted!());
        rig.Rows.Count.ShouldBe(1, "the row is committed whatever happens to the drain enqueue");
    }

    [Fact]
    public async Task EnqueueIntakeAsync_AnyOtherFailureAfterCommit_IsNotSwallowed()
    {
        var rig = new Rig();
        var uow = Substitute.For<IUnitOfWork>();
        Func<Task>? onCompleted = null;
        uow.When(u => u.OnCompleted(Arg.Any<Func<Task>>())).Do(ci => onCompleted = ci.Arg<Func<Task>>());
        rig.UnitOfWorkManager.Current.Returns(uow);
        rig.Jobs.EnqueueAsync(Arg.Any<IntegrationOutboxDrainArgs>(), Arg.Any<BackgroundJobPriority>(), Arg.Any<TimeSpan?>())
            .Returns(Task.FromException<string>(new InvalidOperationException("TEST-job store down")));

        await rig.Build().EnqueueIntakeAsync(Guid.NewGuid(), OfficeId);

        var thrown = await Should.ThrowAsync<InvalidOperationException>(() => onCompleted!());
        thrown.Message.ShouldBe("TEST-job store down");
    }
}
