using System;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Integration.CaseTracker.Jobs;
using NSubstitute;
using Shouldly;
using Volo.Abp.DistributedLocking;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Unit tests for <see cref="IntegrationOutboxDrainJob"/>'s one-pass-per-office rule (#917). During an
/// outage a pass runs long, and Hangfire queues overlapping jobs rather than skipping them, so without
/// the lock the 5-minute kicks would stack passes against a service that is already down. All fixture
/// data is synthetic.
/// </summary>
public class IntegrationOutboxDrainJobTests
{
    private static readonly Guid OfficeId = new("b8844bba-414c-e238-4a71-3a22841f21af");

    private sealed class Harness
    {
        public required IntegrationOutboxDrainJob Job { get; init; }
        public required IntegrationOutboxDrainService Drain { get; init; }
        public required IAbpDistributedLock Lock { get; init; }
        public required ICurrentTenant CurrentTenant { get; init; }
        public required IAbpDistributedLockHandle Handle { get; init; }
    }

    /// <param name="lockHeld">True when another pass for the office already holds the lock.</param>
    private static Harness Build(bool lockHeld)
    {
        // Only DrainDueAsync is exercised, and it is virtual; the constructor just stores its arguments.
        var drain = Substitute.For<IntegrationOutboxDrainService>(null, null, null, null, null, null, null);
        drain.DrainDueAsync(Arg.Any<int?>()).Returns(Task.FromResult(new IntegrationDrainResult(0, 0)));

        var handle = Substitute.For<IAbpDistributedLockHandle>();
        var distributedLock = Substitute.For<IAbpDistributedLock>();
        distributedLock.TryAcquireAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(lockHeld ? null : handle));

        var currentTenant = Substitute.For<ICurrentTenant>();

        return new Harness
        {
            Job = new IntegrationOutboxDrainJob(drain, currentTenant, distributedLock),
            Drain = drain,
            Lock = distributedLock,
            CurrentTenant = currentTenant,
            Handle = handle,
        };
    }

    [Fact]
    public async Task WhenAnotherPassHoldsTheOfficeLock_TheJobSkipsWithoutDraining()
    {
        var h = Build(lockHeld: true);

        await h.Job.ExecuteAsync(new IntegrationOutboxDrainArgs { TenantId = OfficeId });

        await h.Drain.DidNotReceiveWithAnyArgs().DrainDueAsync(default);
    }

    [Fact]
    public async Task WhenTheLockIsFree_TheJobDrainsInsideTheOfficeScope_AndReleasesTheLock()
    {
        var h = Build(lockHeld: false);

        await h.Job.ExecuteAsync(new IntegrationOutboxDrainArgs { TenantId = OfficeId });

        await h.Drain.Received(1).DrainDueAsync(Arg.Any<int?>());
        // Hangfire workers boot with no ambient office; without this the tenant filter hides every row.
        h.CurrentTenant.Received(1).Change(OfficeId, Arg.Any<string?>());
        await h.Handle.Received(1).DisposeAsync();
    }

    [Fact]
    public async Task TheLockIsPerOffice_AndIsTriedWithoutWaiting()
    {
        // Zero wait is the "skip" in skip-if-running: any wait would queue the pass behind the running one.
        var h = Build(lockHeld: false);

        await h.Job.ExecuteAsync(new IntegrationOutboxDrainArgs { TenantId = OfficeId });

        await h.Lock.Received(1).TryAcquireAsync(
            "CaseTracker:OutboxDrain:b8844bba-414c-e238-4a71-3a22841f21af",
            TimeSpan.Zero,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void TheHostHasItsOwnLockName()
    {
        IntegrationOutboxDrainJob.LockName(null).ShouldBe("CaseTracker:OutboxDrain:host");
        IntegrationOutboxDrainJob.LockName(OfficeId).ShouldNotBe(IntegrationOutboxDrainJob.LockName(Guid.NewGuid()));
    }
}
