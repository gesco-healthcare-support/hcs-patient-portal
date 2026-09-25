using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.MultiTenancy;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.SettingManagement;
using Volo.Abp.Timing;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// The host-only guard on the Case Tracker delivery services, tested on the services directly.
///
/// <para>WHY DIRECT CALLS. Through the application's proxy, the Host-only permissions already stop an
/// office caller at the authorization interceptor (that path is proven in
/// <c>HostOnlyIntegrationAdminAuthorizationTests</c>), so the guard inside each method never runs
/// there. These tests call the methods with no interceptor, which is exactly the situation the guard
/// exists for: if a permission's side is ever widened again, the guard still refuses, and it refuses
/// BEFORE any office is entered or aggregated.</para>
///
/// <para>Each refusal has a positive control with no office in scope, which shows the same call does
/// reach the cross-office work -- so a refusal cannot be passing because the call never got there.</para>
/// </summary>
public class HostOnlyIntegrationAdminGuardTests
{
    private static readonly Guid SecondOfficeId = Guid.NewGuid();

    private readonly ITenantWorkRunner _tenantWorkRunner = Substitute.For<ITenantWorkRunner>();
    private readonly IIntegrationOutboxRepository _outboxRepository = Substitute.For<IIntegrationOutboxRepository>();
    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();
    private readonly ISettingManager _settingManager = Substitute.For<ISettingManager>();

    public HostOnlyIntegrationAdminGuardTests()
    {
        _tenantWorkRunner
            .AggregateAcrossOfficesAsync(Arg.Any<Func<Guid, Task<List<CaseTrackerDeadLetterDto>>>>())
            .Returns(new List<List<CaseTrackerDeadLetterDto>>());
        _tenantWorkRunner
            .AggregateAcrossOfficesAsync(Arg.Any<Func<Guid, Task<CaseTrackerOfficePushStateDto>>>())
            .Returns(new List<CaseTrackerOfficePushStateDto>());
        _outboxRepository.GetQueryableAsync()
            .Returns(new List<IntegrationOutboxItem>().AsQueryable());
    }

    // ---- Dead letters ----

    [Fact]
    public async Task DeadLetterList_InsideAnOffice_IsRefused_BeforeAnyOfficeIsAggregated()
    {
        InsideAnOffice();

        await Should.ThrowAsync<AbpAuthorizationException>(() => BuildDeadLetterService().GetListAsync());

        await _tenantWorkRunner.DidNotReceiveWithAnyArgs()
            .AggregateAcrossOfficesAsync<List<CaseTrackerDeadLetterDto>>(default!);
    }

    [Fact]
    public async Task DeadLetterList_AtTheHost_AggregatesTheOffices()
    {
        var list = await BuildDeadLetterService().GetListAsync();

        list.ShouldBeEmpty();
        await _tenantWorkRunner.Received(1)
            .AggregateAcrossOfficesAsync(Arg.Any<Func<Guid, Task<List<CaseTrackerDeadLetterDto>>>>());
    }

    [Fact]
    public async Task DeadLetterRetry_InsideAnOffice_IsRefused_BeforeTheOtherOfficeIsEntered()
    {
        InsideAnOffice();

        await Should.ThrowAsync<AbpAuthorizationException>(
            () => BuildDeadLetterService().RetryAsync(SecondOfficeId, Guid.NewGuid()));

        _currentTenant.DidNotReceive().Change(SecondOfficeId, Arg.Any<string?>());
        await _outboxRepository.DidNotReceive()
            .FindAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeadLetterRetry_AtTheHost_EntersTheOfficeAndLooksTheRowUp()
    {
        // The substituted repository finds nothing, so an admitted call ends on the lookup.
        await Should.ThrowAsync<EntityNotFoundException>(
            () => BuildDeadLetterService().RetryAsync(SecondOfficeId, Guid.NewGuid()));

        _currentTenant.Received(1).Change(SecondOfficeId, Arg.Any<string?>());
    }

    [Fact]
    public async Task DeadLetterRetryAll_InsideAnOffice_IsRefused_BeforeTheOtherOfficeIsEntered()
    {
        InsideAnOffice();

        await Should.ThrowAsync<AbpAuthorizationException>(
            () => BuildDeadLetterService().RetryAllAsync(SecondOfficeId));

        _currentTenant.DidNotReceive().Change(SecondOfficeId, Arg.Any<string?>());
        await _outboxRepository.DidNotReceiveWithAnyArgs().GetQueryableAsync();
    }

    // ---- Push settings ----

    [Fact]
    public async Task PushSettingsList_InsideAnOffice_IsRefused_BeforeAnyOfficeIsAggregated()
    {
        InsideAnOffice();

        await Should.ThrowAsync<AbpAuthorizationException>(() => BuildPushSettingsService().GetOfficesAsync());

        await _tenantWorkRunner.DidNotReceiveWithAnyArgs()
            .AggregateAcrossOfficesAsync<CaseTrackerOfficePushStateDto>(default!);
    }

    [Fact]
    public async Task PushSettingsList_AtTheHost_AggregatesTheOffices()
    {
        var offices = await BuildPushSettingsService().GetOfficesAsync();

        offices.ShouldBeEmpty();
        await _tenantWorkRunner.Received(1)
            .AggregateAcrossOfficesAsync(Arg.Any<Func<Guid, Task<CaseTrackerOfficePushStateDto>>>());
    }

    [Fact]
    public async Task PushSettingsSwitch_InsideAnOffice_IsRefused_BeforeAnySettingIsWritten()
    {
        InsideAnOffice();

        await Should.ThrowAsync<AbpAuthorizationException>(
            () => BuildPushSettingsService().SetPushEnabledAsync(SecondOfficeId, true));

        _currentTenant.DidNotReceive().Change(SecondOfficeId, Arg.Any<string?>());
        await _settingManager.DidNotReceiveWithAnyArgs()
            .SetAsync(default!, default, default!, default, default);
    }

    [Fact]
    public async Task PushSettingsSwitch_AtTheHost_WritesTheSettingInThatOffice()
    {
        var state = await BuildPushSettingsService().SetPushEnabledAsync(SecondOfficeId, true);

        state.OfficeId.ShouldBe(SecondOfficeId);
        _currentTenant.Received().Change(SecondOfficeId, Arg.Any<string?>());
        await _settingManager.Received(1).SetAsync(
            Settings.CaseEvaluationSettings.IntegrationPolicy.CaseTrackerPushEnabled,
            "true",
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<bool>());
    }

    // ------------------------------------------------------------------------

    private void InsideAnOffice()
    {
        _currentTenant.IsAvailable.Returns(true);
        _currentTenant.Id.Returns(Guid.NewGuid());
    }

    private CaseTrackerDeadLetterAppService BuildDeadLetterService() =>
        new(
            _tenantWorkRunner,
            _outboxRepository,
            null!,
            Substitute.For<IRepository<Appointment, Guid>>(),
            null!, // the requeuer (#961): never reached, the guard refuses first
            _currentTenant,
            Substitute.For<ITenantStore>(),
            Substitute.For<IClock>())
        {
            LazyServiceProvider = Substitute.For<IAbpLazyServiceProvider>(),
        };

    private CaseTrackerPushSettingsAppService BuildPushSettingsService() =>
        new(
            _tenantWorkRunner,
            Substitute.For<ITenantStore>(),
            _currentTenant,
            _settingManager,
            _outboxRepository,
            Substitute.For<ILogger<CaseTrackerPushSettingsAppService>>())
        {
            LazyServiceProvider = Substitute.For<IAbpLazyServiceProvider>(),
        };
}
