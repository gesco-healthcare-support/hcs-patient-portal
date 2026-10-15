using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.MultiTenancy;
using HealthcareSupport.CaseEvaluation.TestData;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Guids;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.SettingManagement;
using Volo.Abp.Timing;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// The cutover actions on the host settings surface (#927) against the real settings store and the real feed
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

    private Task<CaseTrackerOfficePushStateDto> SetPushAsync(Guid officeId, bool enabled) =>
        WithUnitOfWorkAsync(() => BuildService().SetPushEnabledAsync(officeId, enabled));

    private Task<CaseTrackerOfficePushStateDto> StartFeedAsync(Guid officeId) =>
        WithUnitOfWorkAsync(() => BuildService().StartFeedAsync(officeId));

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
    public async Task StartFeedAsync_PutsTheOfficeOnTheFeed_AtTheStoresStartFloor()
    {
        var officeId = TenantsTestData.TenantARef;
        await SetPushAsync(officeId, true);

        var dto = await StartFeedAsync(officeId);

        dto.FeedActive.ShouldBeTrue();
        dto.PushEnabled.ShouldBeTrue(); // stays on: reconcile and attendance still need it
        dto.OutstandingCount.ShouldBe(3);
        var record = await FeedRecordAsync(officeId);
        record!.IsActive.ShouldBeTrue();
        record.FloorPosition.ShouldBe(StartFloor);
        record.AcknowledgedPosition.ShouldBe(StartFloor);
    }

    [Fact]
    public async Task StartFeedAsync_WhenAlreadyOn_IsRefused_AndTheFloorDoesNotMove()
    {
        var officeId = TenantsTestData.TenantARef;
        await SetPushAsync(officeId, true);
        await StartFeedAsync(officeId);
        _store.GetStartFloorAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(900L));

        await Should.ThrowAsync<UserFriendlyException>(() => StartFeedAsync(officeId));

        (await FeedRecordAsync(officeId))!.FloorPosition.ShouldBe(StartFloor);
    }

    [Fact]
    public async Task StartFeedAsync_WithThePushSwitchOff_IsRefused_AndWritesNothing()
    {
        // The push switch is the office's ePHI gate; the feed refuses an office that has it off, so starting one
        // there would only look as if it worked.
        var officeId = TenantsTestData.TenantARef;
        await SetPushAsync(officeId, false);

        await Should.ThrowAsync<UserFriendlyException>(() => StartFeedAsync(officeId));

        (await FeedRecordAsync(officeId)).ShouldBeNull();
    }

    [Fact]
    public async Task ReturnToPushAsync_TakesTheOfficeOffTheFeed_AndTheDrainMayPushAgain()
    {
        var officeId = TenantsTestData.TenantARef;
        await SetPushAsync(officeId, true);
        await StartFeedAsync(officeId);

        var dto = await WithUnitOfWorkAsync(() => BuildService().ReturnToPushAsync(officeId));

        dto.FeedActive.ShouldBeFalse();
        dto.PushEnabled.ShouldBeTrue();
        dto.OutstandingCount.ShouldBeNull();
        (await FeedRecordAsync(officeId))!.IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task ReturnToPushAsync_WhenNotOnTheFeed_IsRefused()
    {
        var officeId = TenantsTestData.TenantARef;
        await SetPushAsync(officeId, true);

        await Should.ThrowAsync<UserFriendlyException>(
            () => WithUnitOfWorkAsync(() => BuildService().ReturnToPushAsync(officeId)));
    }

    [Fact]
    public async Task GetOfficesAsync_ShowsEachOfficesMode_AndReadsTheStoreOnlyForAFedOffice()
    {
        await SetPushAsync(TenantsTestData.TenantARef, true);
        await StartFeedAsync(TenantsTestData.TenantARef);
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
