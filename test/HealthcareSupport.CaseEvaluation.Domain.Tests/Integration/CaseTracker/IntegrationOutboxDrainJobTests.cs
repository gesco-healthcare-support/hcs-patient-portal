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
/// the lock the 5-minute kicks would stack passes against a service that is already down.
///
/// <para>Also pinned: the drain runs INSIDE the office's tenant scope and the scope is closed afterwards,
/// even when the drain throws. Background workers start with no ambient tenant, so a drain outside the
/// scope would read another database's rows -- or none -- and push nothing. All fixture data is
/// synthetic.</para>
/// </summary>
public class IntegrationOutboxDrainJobTests
{
    private static readonly Guid OfficeId = new("0ff1ce00-0000-4000-8000-000000000001");

    private sealed class Harness
    {
        public required IntegrationOutboxDrainJob Job { get; init; }
        public required IntegrationOutboxDrainService Drain { get; init; }
        public required IAbpDistributedLock Lock { get; init; }
        public required ICurrentTenant CurrentTenant { get; init; }
        public required IAbpDistributedLockHandle Handle { get; init; }
        public required IDisposable TenantScope { get; init; }
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

        var tenantScope = Substitute.For<IDisposable>();
        var currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.Change(Arg.Any<Guid?>(), Arg.Any<string?>()).Returns(tenantScope);

        return new Harness
        {
            Job = new IntegrationOutboxDrainJob(drain, currentTenant, distributedLock),
            Drain = drain,
            Lock = distributedLock,
            CurrentTenant = currentTenant,
            Handle = handle,
            TenantScope = tenantScope,
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
            "CaseTracker:OutboxDrain:0ff1ce00-0000-4000-8000-000000000001",
            TimeSpan.Zero,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void TheHostHasItsOwnLockName()
    {
        IntegrationOutboxDrainJob.LockName(null).ShouldBe("CaseTracker:OutboxDrain:host");
        IntegrationOutboxDrainJob.LockName(OfficeId).ShouldNotBe(IntegrationOutboxDrainJob.LockName(Guid.NewGuid()));
    }

    /// <summary>
    /// Enter the office's scope, drain, leave it -- in that order. Both a drain that did work and one
    /// that found nothing are run, covering the job's log branch.
    /// </summary>
    [Theory]
    [InlineData(3, 1)]
    [InlineData(0, 0)]
    public async Task TheDrainRunsBetweenEnteringAndLeavingTheOfficeScope(int sent, int failed)
    {
        var h = Build(lockHeld: false);
        h.Drain.DrainDueAsync(Arg.Any<int?>()).Returns(Task.FromResult(new IntegrationDrainResult(sent, failed)));

        await h.Job.ExecuteAsync(new IntegrationOutboxDrainArgs { TenantId = OfficeId });

        Received.InOrder(() =>
        {
            h.CurrentTenant.Change(OfficeId, Arg.Any<string?>());
            h.Drain.DrainDueAsync(Arg.Any<int?>());
            h.TenantScope.Dispose();
        });
    }

    [Fact]
    public async Task ADrainThatThrows_Propagates_AndStillClosesTheScopeAndReleasesTheLock()
    {
        var h = Build(lockHeld: false);
        h.Drain.DrainDueAsync(Arg.Any<int?>())
            .Returns<IntegrationDrainResult>(_ => throw new InvalidOperationException("TEST-drain failure"));

        var thrown = await Should.ThrowAsync<InvalidOperationException>(
            () => h.Job.ExecuteAsync(new IntegrationOutboxDrainArgs { TenantId = OfficeId }));

        thrown.Message.ShouldBe("TEST-drain failure");
        h.TenantScope.Received(1).Dispose();
        await h.Handle.Received(1).DisposeAsync();
    }
}
