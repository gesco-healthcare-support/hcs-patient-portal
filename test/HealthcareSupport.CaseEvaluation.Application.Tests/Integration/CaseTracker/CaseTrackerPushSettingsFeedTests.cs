using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.MultiTenancy;
using HealthcareSupport.CaseEvaluation.Settings;
using HealthcareSupport.CaseEvaluation.TestData;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Guids;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.SettingManagement;
using Volo.Abp.Timing;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Connecting and disconnecting an office on the host settings surface (#927, 2026-10-26) against the real settings store and the real feed
/// record, on the SQLite rig. The SQL-Server-only store is substituted: its start floor and outstanding count
/// are fixed here and proved on SQL Server separately. The service is built by hand, so the authorization
/// interceptor does not run: what is under test is what the actions DO.
/// </summary>
public abstract class CaseTrackerPushSettingsFeedTests<TStartupModule> : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private const long StartFloor = 500;

    private readonly ICurrentTenant _currentTenant;
    private readonly ICaseTrackerFeedStore _store;

    protected CaseTrackerPushSettingsFeedTests()
    {
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _store = Substitute.For<ICaseTrackerFeedStore>();
        _store.GetStartFloorAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(StartFloor));
        _store.CountOutstandingAsync(Arg.Any<Guid>(), Arg.Any<long>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(3));
    }

    private CaseTrackerPushSettingsAppService BuildService() =>
        new(
            GetRequiredService<ITenantWorkRunner>(),
            GetRequiredService<ITenantStore>(),
            _currentTenant,
            GetRequiredService<ISettingManager>(),
            GetRequiredService<IIntegrationOutboxRepository>(),
            new CaseTrackerFeedManager(
                GetRequiredService<ICaseTrackerFeedStateRepository>(),
                _store,
                GetRequiredService<IClock>(),
                GetRequiredService<IGuidGenerator>()),
            NullLogger<CaseTrackerPushSettingsAppService>.Instance)
        {
            LazyServiceProvider = GetRequiredService<IAbpLazyServiceProvider>(),
        };

    private Task<CaseTrackerOfficePushStateDto> ConnectAsync(Guid officeId) =>
        WithUnitOfWorkAsync(() => BuildService().ConnectAsync(officeId));

    private Task<CaseTrackerOfficePushStateDto> DisconnectAsync(Guid officeId) =>
        WithUnitOfWorkAsync(() => BuildService().DisconnectAsync(officeId));

    /// <summary>Writes the switch directly, as an office set up before Connect existed could have it.</summary>
    private Task WriteSwitchAsync(Guid officeId, string value) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(officeId))
            {
                await GetRequiredService<ISettingManager>().SetForCurrentTenantAsync(
                    CaseEvaluationSettings.IntegrationPolicy.CaseTrackerPushEnabled, value);
            }
        });

    private async Task<CaseTrackerFeedState?> FeedRecordAsync(Guid officeId)
    {
        CaseTrackerFeedState? state = null;
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(officeId))
            {
                state = await GetRequiredService<ICaseTrackerFeedStateRepository>().FindCurrentAsync();
            }
        });
        return state;
    }

    [Fact]
    public async Task ConnectAsync_StartsTheFeedAtTheStoresStartFloor_AndTurnsTheSwitchOn()
    {
        var officeId = TenantsTestData.TenantARef;

        var dto = await ConnectAsync(officeId);

        dto.PushEnabled.ShouldBeTrue(); // the switch also gates reconcile and attendance coming back
        dto.FeedActive.ShouldBeTrue();
        dto.OutstandingCount.ShouldBe(3);
        var record = await FeedRecordAsync(officeId);
        record!.IsActive.ShouldBeTrue();
        record.FloorPosition.ShouldBe(StartFloor);
        record.AcknowledgedPosition.ShouldBe(StartFloor);
    }

    [Fact]
    public async Task ConnectAsync_AnOfficeSwitchedOnWithoutTheFeed_StartsTheFeed()
    {
        // The state "Turn on" alone used to leave: push mode, with nowhere to push on Azure.
        var officeId = TenantsTestData.TenantARef;
        await WriteSwitchAsync(officeId, "true");

        var dto = await ConnectAsync(officeId);

        dto.PushEnabled.ShouldBeTrue();
        dto.FeedActive.ShouldBeTrue();
        (await FeedRecordAsync(officeId))!.FloorPosition.ShouldBe(StartFloor);
    }

    [Fact]
    public async Task ConnectAsync_AnOfficeWhoseFeedIsOnButSwitchedOff_OnlyTurnsTheSwitchOn_AndKeepsItsPosition()
    {
        // The Sandbox state on 2026-10-26: feed active, switch off. Its feed resumes where the Case Tracker
        // last acknowledged; the floor is not moved, so nothing waiting is skipped or sent twice.
        var officeId = TenantsTestData.TenantARef;
        await ConnectAsync(officeId);
        await DisconnectAsync(officeId);
        _store.GetStartFloorAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(900L));

        var dto = await ConnectAsync(officeId);

        dto.PushEnabled.ShouldBeTrue();
        dto.FeedActive.ShouldBeTrue();
        var record = await FeedRecordAsync(officeId);
        record!.FloorPosition.ShouldBe(StartFloor);
        record.AcknowledgedPosition.ShouldBe(StartFloor);
    }

    [Fact]
    public async Task ConnectAsync_AConnectedOffice_ChangesNothing()
    {
        var officeId = TenantsTestData.TenantARef;
        await ConnectAsync(officeId);
        _store.GetStartFloorAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(900L));

        var dto = await ConnectAsync(officeId);

        dto.PushEnabled.ShouldBeTrue();
        dto.FeedActive.ShouldBeTrue();
        (await FeedRecordAsync(officeId))!.FloorPosition.ShouldBe(StartFloor);
    }

    [Fact]
    public async Task DisconnectAsync_TurnsTheSwitchOff_AndLeavesTheFeedAsItIs()
    {
        var officeId = TenantsTestData.TenantARef;
        await ConnectAsync(officeId);

        var dto = await DisconnectAsync(officeId);

        dto.PushEnabled.ShouldBeFalse();
        dto.FeedActive.ShouldBeTrue(); // the feed refuses the office while its switch is off; changes wait
        var record = await FeedRecordAsync(officeId);
        record!.IsActive.ShouldBeTrue();
        record.FloorPosition.ShouldBe(StartFloor);
    }

    [Fact]
    public async Task DisconnectAsync_AnOfficeNeverConnected_StartsNoFeed()
    {
        var officeId = TenantsTestData.TenantARef;

        var dto = await DisconnectAsync(officeId);

        dto.PushEnabled.ShouldBeFalse();
        dto.FeedActive.ShouldBeFalse();
        (await FeedRecordAsync(officeId)).ShouldBeNull();
    }

    [Fact]
    public async Task GetOfficesAsync_ShowsEachOfficesMode_AndReadsTheStoreOnlyForAFedOffice()
    {
        await ConnectAsync(TenantsTestData.TenantARef);
        _store.ClearReceivedCalls();

        var offices = await WithUnitOfWorkAsync(() => BuildService().GetOfficesAsync());

        var fed = offices.Single(o => o.OfficeId == TenantsTestData.TenantARef);
        fed.FeedActive.ShouldBeTrue();
        fed.OutstandingCount.ShouldBe(3);
        var onPush = offices.Single(o => o.OfficeId == TenantsTestData.TenantBRef);
        onPush.FeedActive.ShouldBeFalse();
        onPush.OutstandingCount.ShouldBeNull();
        await _store.Received(1).CountOutstandingAsync(TenantsTestData.TenantARef, Arg.Any<long>(), Arg.Any<CancellationToken>());
    }
}
