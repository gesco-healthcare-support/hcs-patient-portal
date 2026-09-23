using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Integration.CaseTracker.Jobs;
using NSubstitute;
using Shouldly;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Unit coverage for <see cref="IntegrationOutboxDrainJob"/>, the background job that pushes one
/// office's due Case Tracker rows.
///
/// <para><b>WHAT IS PINNED.</b> The drain runs INSIDE the office's tenant scope and the scope is closed
/// afterwards. Background workers start with no ambient tenant, so a drain outside the scope would
/// read another database's rows -- or none -- and push nothing.</para>
///
/// <para><b>The result asserted is the call order</b>: enter the office's scope, drain, leave it.
/// <see cref="IntegrationOutboxDrainService"/> is substituted (its constructor only stores its
/// arguments and <c>DrainDueAsync</c> is virtual), so nothing is read or pushed. Both a drain that did
/// work and one that found nothing are run, covering the job's log branch.</para>
/// </summary>
public class IntegrationOutboxDrainJobTests
{
    private static readonly Guid OfficeId = new("dddddddd-0000-0000-0000-00000000000d");

    [Theory]
    [InlineData(3, 1)]
    [InlineData(0, 0)]
    public async Task ExecuteAsync_DrainsInsideTheOfficesTenantScopeAndClosesItAfter(int sent, int failed)
    {
        var drain = Substitute.For<IntegrationOutboxDrainService>(null, null, null, null, null, null);
        drain.DrainDueAsync(Arg.Any<int?>()).Returns(new IntegrationDrainResult(sent, failed));
        var scope = Substitute.For<IDisposable>();
        var currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.Change(Arg.Any<Guid?>(), Arg.Any<string?>()).Returns(scope);

        await new IntegrationOutboxDrainJob(drain, currentTenant).ExecuteAsync(new IntegrationOutboxDrainArgs { TenantId = OfficeId });

        Received.InOrder(() =>
        {
            currentTenant.Change(OfficeId, Arg.Any<string?>());
            drain.DrainDueAsync(Arg.Any<int?>());
            scope.Dispose();
        });
    }

    [Fact]
    public async Task ExecuteAsync_ADrainThatThrows_PropagatesAndStillClosesTheScope()
    {
        var drain = Substitute.For<IntegrationOutboxDrainService>(null, null, null, null, null, null);
        drain.DrainDueAsync(Arg.Any<int?>())
            .Returns<IntegrationDrainResult>(_ => throw new InvalidOperationException("TEST-drain failure"));
        var scope = Substitute.For<IDisposable>();
        var currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.Change(Arg.Any<Guid?>(), Arg.Any<string?>()).Returns(scope);

        var thrown = await Should.ThrowAsync<InvalidOperationException>(
            () => new IntegrationOutboxDrainJob(drain, currentTenant).ExecuteAsync(new IntegrationOutboxDrainArgs { TenantId = OfficeId }));

        thrown.Message.ShouldBe("TEST-drain failure");
        scope.Received(1).Dispose();
    }
}
