using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Integration.CaseTracker;
using HealthcareSupport.CaseEvaluation.Permissions;
using HealthcareSupport.CaseEvaluation.Security;
using HealthcareSupport.CaseEvaluation.Settings;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Volo.Abp.Security.Claims;
using Volo.Abp.SettingManagement;
using Volo.Abp.Timing;
using Volo.Saas.Tenants;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.RealAuthorization;

/// <summary>
/// The Case Tracker delivery screens are host-only: listing failed pushes, retrying one, and reading
/// or switching an office's push setting all act on EVERY office, so a caller inside an office must
/// be refused -- before any of that work starts -- while a host operator is admitted.
///
/// <para>THE FIXTURE IS DELIBERATELY NOT EMPTY. A second office holds one failed push and a push
/// setting of "true", and every refusal below re-reads both afterwards. A refusal proven against an
/// office with nothing in it would pass whether or not the other office was reachable.</para>
///
/// <para>THE OFFICE CALLER HOLDS THE GRANTS AN OFFICE ADMIN HAD BEFORE THIS CHANGE. They are written
/// as rows straight into the permission-grant table, because the permission manager now refuses to
/// grant a host-only permission inside an office. That is the state a deployed office database is
/// in, so these tests also show that those existing rows open nothing.</para>
/// </summary>
[Collection(RealAuthorizationCollection.Name)]
public class HostOnlyIntegrationAdminAuthorizationTests : CaseEvaluationRealAuthorizationTestBase
{
    private const string SecondOfficeName = "F2-authz-office-b";
    private const string OfficeAdminRoleName = "F2-authz-office-admin";
    private const string HostOperatorRoleName = "F2-authz-host-operator";
    private const string RoleProviderName = "R";

    private static readonly SemaphoreSlim SecondOfficeLock = new(1, 1);
    private static SecondOffice? _secondOffice;

    private readonly ICurrentPrincipalAccessor _principalAccessor;
    private readonly ICurrentTenant _currentTenant;

    public HostOnlyIntegrationAdminAuthorizationTests()
    {
        _principalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    // ---- An office caller is refused, and the second office is untouched ----

    [Fact]
    public async Task DeadLetterList_IsRefused_ForAnOfficeCaller()
    {
        var second = await EnsureSecondOfficeAsync();

        await AssertRefusedInOfficeAsync(
            sp => sp.GetRequiredService<ICaseTrackerDeadLetterAppService>().GetListAsync());

        await AssertSecondOfficeUntouchedAsync(second);
    }

    [Fact]
    public async Task DeadLetterRetry_IsRefused_ForAnOfficeCaller_TargetingAnotherOffice()
    {
        var second = await EnsureSecondOfficeAsync();

        await AssertRefusedInOfficeAsync(
            sp => sp.GetRequiredService<ICaseTrackerDeadLetterAppService>()
                .RetryAsync(second.OfficeId, second.DeadLetterId));

        await AssertSecondOfficeUntouchedAsync(second);
    }

    [Fact]
    public async Task DeadLetterRetryAll_IsRefused_ForAnOfficeCaller_TargetingAnotherOffice()
    {
        // Retry-all acts on whichever office its route names, so an office caller naming another office
        // must be refused before that office is entered -- and its dead letter must still be Failed.
        var second = await EnsureSecondOfficeAsync();

        await AssertRefusedInOfficeAsync(
            sp => sp.GetRequiredService<ICaseTrackerDeadLetterAppService>().RetryAllAsync(second.OfficeId));

        await AssertSecondOfficeUntouchedAsync(second);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FeedActions_AreRefused_ForAnOfficeCaller_TargetingAnotherOffice(bool start)
    {
        // #927: the cutover actions switch how another office's changes reach the Case Tracker.
        var second = await EnsureSecondOfficeAsync();

        await AssertRefusedInOfficeAsync(sp =>
        {
            var service = sp.GetRequiredService<ICaseTrackerPushSettingsAppService>();
            return start ? service.StartFeedAsync(second.OfficeId) : service.ReturnToPushAsync(second.OfficeId);
        });

        await AssertSecondOfficeUntouchedAsync(second);
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(second.OfficeId))
            {
                (await GetRequiredService<ICaseTrackerFeedStateRepository>().FindCurrentAsync()).ShouldBeNull();
            }
        }, requiresNew: true);
    }

    [Fact]
    public async Task PushSettingsList_IsRefused_ForAnOfficeCaller()
    {
        var second = await EnsureSecondOfficeAsync();

        await AssertRefusedInOfficeAsync(
            sp => sp.GetRequiredService<ICaseTrackerPushSettingsAppService>().GetOfficesAsync());

        await AssertSecondOfficeUntouchedAsync(second);
    }

    [Fact]
    public async Task PushSettingsSwitch_IsRefused_ForAnOfficeCaller_TargetingAnotherOffice()
    {
        var second = await EnsureSecondOfficeAsync();

        await AssertRefusedInOfficeAsync(
            sp => sp.GetRequiredService<ICaseTrackerPushSettingsAppService>()
                .SetPushEnabledAsync(second.OfficeId, false));

        await AssertSecondOfficeUntouchedAsync(second);
    }

    // ---- Positive controls: a host operator is admitted. Without these, a harness that refused
    // everyone would make every refusal above pass. ----

    [Fact]
    public async Task DeadLetterList_IsAdmitted_ForAHostOperator_AndSeesTheSecondOfficesFailure()
    {
        var second = await EnsureSecondOfficeAsync();

        var list = await RunAsHostOperatorAsync(
            sp => sp.GetRequiredService<ICaseTrackerDeadLetterAppService>().GetListAsync());

        // Also proves the fixture is not empty: the row every refusal re-reads is really there.
        var row = list.Where(r => r.Id == second.DeadLetterId).ShouldHaveSingleItem();
        row.OfficeId.ShouldBe(second.OfficeId);
    }

    [Fact]
    public async Task DeadLetterRetry_IsAdmitted_ForAHostOperator()
    {
        var second = await EnsureSecondOfficeAsync();

        // An unknown row id: an admitted call reaches the method body and fails on the lookup;
        // a refused one would throw AbpAuthorizationException instead.
        await Should.ThrowAsync<EntityNotFoundException>(() => RunAsHostOperatorAsync(
            sp => sp.GetRequiredService<ICaseTrackerDeadLetterAppService>()
                .RetryAsync(second.OfficeId, Guid.NewGuid())));
    }

    [Fact]
    public async Task DeadLetterRetryAll_IsAdmitted_ForAHostOperator()
    {
        // No office id: an admitted call reaches the method body and is refused there as a bad request; a
        // refused one would throw AbpAuthorizationException instead. Nothing is retried either way.
        await Should.ThrowAsync<UserFriendlyException>(() => RunAsHostOperatorAsync(
            sp => sp.GetRequiredService<ICaseTrackerDeadLetterAppService>().RetryAllAsync(Guid.Empty)));
    }

    [Fact]
    public async Task FeedReturn_IsAdmitted_ForAHostOperator()
    {
        // The second office is not on the feed, so an admitted call reaches the method body and is refused
        // there; a refused one would throw AbpAuthorizationException instead. Nothing changes either way.
        var second = await EnsureSecondOfficeAsync();

        await Should.ThrowAsync<UserFriendlyException>(() => RunAsHostOperatorAsync(
            sp => sp.GetRequiredService<ICaseTrackerPushSettingsAppService>().ReturnToPushAsync(second.OfficeId)));
    }

    [Fact]
    public async Task PushSettingsList_IsAdmitted_ForAHostOperator()
    {
        var second = await EnsureSecondOfficeAsync();

        var offices = await RunAsHostOperatorAsync(
            sp => sp.GetRequiredService<ICaseTrackerPushSettingsAppService>().GetOfficesAsync());

        var state = offices.Where(o => o.OfficeId == second.OfficeId).ShouldHaveSingleItem();
        state.PushEnabled.ShouldBeTrue();
    }

    [Fact]
    public async Task PushSettingsSwitch_IsAdmitted_ForAHostOperator()
    {
        var second = await EnsureSecondOfficeAsync();

        // Writes the value the office already has, so the shared fixture is left as it was.
        var state = await RunAsHostOperatorAsync(
            sp => sp.GetRequiredService<ICaseTrackerPushSettingsAppService>()
                .SetPushEnabledAsync(second.OfficeId, true));

        state.OfficeId.ShouldBe(second.OfficeId);
        state.PushEnabled.ShouldBeTrue();
    }

    // ------------------------------------------------------------------------

    private async Task AssertRefusedInOfficeAsync(Func<IServiceProvider, Task> call)
    {
        var fixture = await GetFixtureAsync();

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(fixture.Office.OfficeId))
            using (WithCurrentUser.Run(_principalAccessor, Guid.NewGuid(), OfficeAdminRoleName))
            {
                await Should.ThrowAsync<AbpAuthorizationException>(() => call(ServiceProvider));
            }
        }, requiresNew: true);
    }

    private Task<T> RunAsHostOperatorAsync<T>(Func<IServiceProvider, Task<T>> call)
    {
        return WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(null))
            using (WithCurrentUser.Run(_principalAccessor, Guid.NewGuid(), HostOperatorRoleName))
            {
                return await call(ServiceProvider);
            }
        }, requiresNew: true);
    }

    /// <summary>Re-reads the second office: still exactly one outbox row, still failed, push still on.</summary>
    private async Task AssertSecondOfficeUntouchedAsync(SecondOffice second)
    {
        var outbox = GetRequiredService<IIntegrationOutboxRepository>();
        var settings = GetRequiredService<ISettingManager>();

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(second.OfficeId))
            {
                var rows = await outbox.GetListAsync();
                var row = rows.ShouldHaveSingleItem();
                row.Id.ShouldBe(second.DeadLetterId);
                row.Status.ShouldBe(IntegrationOutboxStatus.Failed);

                var push = await settings.GetOrNullForCurrentTenantAsync(
                    CaseEvaluationSettings.IntegrationPolicy.CaseTrackerPushEnabled);
                push.ShouldBe("true");
            }
        }, requiresNew: true);
    }

    /// <summary>
    /// Creates the second office with its failed push and push setting, the office-admin role in the
    /// shared office holding the pre-change grants, and the host operator role. Once per process.
    /// </summary>
    private async Task<SecondOffice> EnsureSecondOfficeAsync()
    {
        if (_secondOffice != null)
        {
            return _secondOffice;
        }

        var fixture = await GetFixtureAsync();

        await SecondOfficeLock.WaitAsync();
        try
        {
            if (_secondOffice != null)
            {
                return _secondOffice;
            }

            var tenantManager = GetRequiredService<ITenantManager>();
            var tenantRepository = GetRequiredService<IRepository<Tenant, Guid>>();
            var outbox = GetRequiredService<IIntegrationOutboxRepository>();
            var settings = GetRequiredService<ISettingManager>();
            var roleManager = GetRequiredService<IdentityRoleManager>();
            var grants = GetRequiredService<IPermissionGrantRepository>();
            var clock = GetRequiredService<IClock>();

            var second = await WithUnitOfWorkAsync(async () =>
            {
                Guid officeId;
                using (_currentTenant.Change(null))
                {
                    var office = await tenantManager.CreateAsync(SecondOfficeName);
                    office.SetDefaultConnectionString(RealAuthorizationTestDatabase.SecondOfficeConnectionString);
                    await tenantRepository.InsertAsync(office, autoSave: true);
                    officeId = office.Id;

                    await roleManager.CreateAsync(new IdentityRole(Guid.NewGuid(), HostOperatorRoleName, tenantId: null));
                    await InsertGrantAsync(grants, CaseEvaluationPermissions.Appointments.ViewIntegrationDeadLetters, HostOperatorRoleName, null);
                    await InsertGrantAsync(grants, CaseEvaluationPermissions.CaseTrackerIntegration.Default, HostOperatorRoleName, null);
                }

                Guid deadLetterId;
                using (_currentTenant.Change(officeId))
                {
                    var failed = new IntegrationOutboxItem(
                        Guid.NewGuid(),
                        officeId,
                        IntegrationMessageType.Intake,
                        "api/intake/appointments",
                        Guid.NewGuid(),
                        "{}",
                        "TEST-host-only-" + Guid.NewGuid().ToString("N"));
                    failed.MarkFatal(clock.Now, "TEST- seeded failure");
                    await outbox.InsertAsync(failed, autoSave: true);
                    deadLetterId = failed.Id;

                    await settings.SetForCurrentTenantAsync(
                        CaseEvaluationSettings.IntegrationPolicy.CaseTrackerPushEnabled, "true");
                }

                var shared = fixture.Office.OfficeId;
                using (_currentTenant.Change(shared))
                {
                    await roleManager.CreateAsync(new IdentityRole(Guid.NewGuid(), OfficeAdminRoleName, shared));
                    await InsertGrantAsync(grants, CaseEvaluationPermissions.Appointments.ViewIntegrationDeadLetters, OfficeAdminRoleName, shared);
                    await InsertGrantAsync(grants, CaseEvaluationPermissions.Appointments.PushToCaseTracker, OfficeAdminRoleName, shared);
                }

                return new SecondOffice(officeId, deadLetterId);
            }, requiresNew: true);

            _secondOffice = second;
            return second;
        }
        finally
        {
            SecondOfficeLock.Release();
        }
    }

    private static Task InsertGrantAsync(
        IPermissionGrantRepository grants, string permission, string roleName, Guid? tenantId)
        => grants.InsertAsync(
            new PermissionGrant(Guid.NewGuid(), permission, RoleProviderName, roleName, tenantId),
            autoSave: true);

    private sealed record SecondOffice(Guid OfficeId, Guid DeadLetterId);
}
