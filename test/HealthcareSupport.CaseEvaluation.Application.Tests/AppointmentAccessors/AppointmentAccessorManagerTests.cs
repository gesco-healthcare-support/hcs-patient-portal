using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using HealthcareSupport.CaseEvaluation.TestData;
using NSubstitute;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Local;
using Volo.Abp.Identity;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentAccessors;

/// <summary>
/// Issue #3 (2026-07-16): <see cref="AppointmentAccessorManager.CreateOrLinkAsync"/>
/// publishes <see cref="AppointmentAccessorAddedEto"/> for an accessor who ALREADY has
/// a tenant account (LinkExisting / GrantRoleAndLink), while the brand-new-account path
/// still publishes <see cref="AppointmentAccessorInvitedEto"/> (and NOT the added-eto).
///
/// <para>Real <see cref="IdentityUserManager"/> + repositories from DI so the outcome
/// resolution runs for real; a substituted <see cref="ILocalEventBus"/> so the publish
/// is asserted directly without running the live email handler.</para>
/// </summary>
public abstract class AppointmentAccessorManagerTests<TStartupModule> : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IdentityUserManager _userManager;
    private readonly IIdentityRoleRepository _roleRepository;
    private readonly IAppointmentAccessorRepository _accessorRepository;
    private readonly ICurrentTenant _currentTenant;

    protected AppointmentAccessorManagerTests()
    {
        _userManager = GetRequiredService<IdentityUserManager>();
        _roleRepository = GetRequiredService<IIdentityRoleRepository>();
        _accessorRepository = GetRequiredService<IAppointmentAccessorRepository>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    private AppointmentAccessorManager BuildManager(ILocalEventBus bus)
    {
        // Manually constructed to inject the substituted event bus, so ABP's property
        // injection does not run. Modern ABP exposes GuidGenerator (and friends) as
        // get-only expressions over LazyServiceProvider, so setting that one public
        // injected property is enough for the code path (GuidGenerator.Create()).
        var manager = new AppointmentAccessorManager(_accessorRepository, _userManager, _roleRepository, bus)
        {
            LazyServiceProvider = GetRequiredService<IAbpLazyServiceProvider>(),
        };
        return manager;
    }

    [Fact]
    public async Task CreateOrLinkAsync_ExistingAccount_PublishesAddedEto_NotInvited()
    {
        var bus = Substitute.For<ILocalEventBus>();

        // TenantAdmin1 exists in TenantA but is not already an accessor on Appointment1;
        // requesting the Applicant Attorney role takes the GrantRoleAndLink (existing-
        // account) branch, which must publish the AddedEto.
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                await BuildManager(bus).CreateOrLinkAsync(
                    appointmentId: AppointmentsTestData.Appointment1Id,
                    email: IdentityUsersTestData.TenantAdmin1Email,
                    requestedRoleName: IdentityUsersTestData.ApplicantAttorneyRoleName,
                    accessTypeId: AccessType.View,
                    tenantId: TenantsTestData.TenantARef);
            }
        });

        await bus.Received(1).PublishAsync(Arg.Any<AppointmentAccessorAddedEto>());
        await bus.DidNotReceive().PublishAsync(Arg.Any<AppointmentAccessorInvitedEto>());
    }

    [Fact]
    public async Task CreateOrLinkAsync_NewAccount_PublishesInvitedEto_NotAdded()
    {
        var bus = Substitute.For<ILocalEventBus>();
        var newEmail = $"TEST-added-{Guid.NewGuid():N}@test.local";

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                await BuildManager(bus).CreateOrLinkAsync(
                    appointmentId: AppointmentsTestData.Appointment1Id,
                    email: newEmail,
                    requestedRoleName: IdentityUsersTestData.ApplicantAttorneyRoleName,
                    accessTypeId: AccessType.View,
                    tenantId: TenantsTestData.TenantARef);
            }
        });

        await bus.Received(1).PublishAsync(Arg.Any<AppointmentAccessorInvitedEto>());
        await bus.DidNotReceive().PublishAsync(Arg.Any<AppointmentAccessorAddedEto>());
    }
}
