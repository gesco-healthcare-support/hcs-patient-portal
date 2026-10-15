using System;
using System.Threading.Tasks;
using NSubstitute;
using Shouldly;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Notifications.Outbox;

/// <summary>
/// Unit coverage for <see cref="OutboxDrainJob"/>, the Hangfire wrapper that drains ONE office's
/// due email outbox rows.
///
/// <para><b>WHAT IS PINNED, and why it matters.</b> The drain runs INSIDE the office's tenant scope,
/// and the scope is closed afterwards. Hangfire workers start with no ambient tenant, so a drain run
/// outside the scope would be hidden from that office's rows by the multi-tenant filter and would
/// silently send nothing.</para>
///
/// <para><b>The result asserted is the call ORDER</b> -- enter the scope for this office, drain,
/// leave the scope. <see cref="OutboxDrainService"/> is substituted (its constructor only stores its
/// arguments and <c>DrainDueAsync</c> is virtual), so no row is read and no mail is sent. Both a
/// drain that sent mail and one that found nothing are run, which covers the job's log branch.</para>
/// </summary>
public class OutboxDrainJobTests
{
    private static readonly Guid OfficeId = new("77777777-7777-7777-7777-777777777777");

    [Theory]
    [InlineData(2, 1)]
    [InlineData(0, 0)]
    public async Task ExecuteAsync_DrainsInsideTheOfficesTenantScopeAndClosesItAfter(int sent, int failed)
    {
        var drain = Substitute.For<OutboxDrainService>(null, null, null, null, null);
        drain.DrainDueAsync(Arg.Any<int?>()).Returns(new OutboxDrainResult(sent, failed));
        var scope = Substitute.For<IDisposable>();
        var currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.Change(Arg.Any<Guid?>(), Arg.Any<string?>()).Returns(scope);

        await new OutboxDrainJob(drain, currentTenant).ExecuteAsync(new OutboxDrainArgs { TenantId = OfficeId });

        Received.InOrder(() =>
        {
            currentTenant.Change(OfficeId, Arg.Any<string?>());
            drain.DrainDueAsync(Arg.Any<int?>());
            scope.Dispose();
        });
    }

    /// <summary>
    /// A drain that throws is NOT swallowed: Hangfire must see the failure to retry the job, and the
    /// tenant scope is still closed.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_ADrainThatThrows_PropagatesAndStillClosesTheScope()
    {
        var drain = Substitute.For<OutboxDrainService>(null, null, null, null, null);
        drain.DrainDueAsync(Arg.Any<int?>())
            .Returns<OutboxDrainResult>(_ => throw new InvalidOperationException("TEST-drain failure"));
        var scope = Substitute.For<IDisposable>();
        var currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.Change(Arg.Any<Guid?>(), Arg.Any<string?>()).Returns(scope);

        var thrown = await Should.ThrowAsync<InvalidOperationException>(
            () => new OutboxDrainJob(drain, currentTenant).ExecuteAsync(new OutboxDrainArgs { TenantId = OfficeId }));

        thrown.Message.ShouldBe("TEST-drain failure");
        scope.Received(1).Dispose();
    }
}
