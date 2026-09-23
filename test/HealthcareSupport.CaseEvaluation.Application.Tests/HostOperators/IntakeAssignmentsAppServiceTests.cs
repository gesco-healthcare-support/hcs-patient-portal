using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Identity;
using HealthcareSupport.CaseEvaluation.Security;
using HealthcareSupport.CaseEvaluation.TestData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.HostOperators;

/// <summary>
/// The host intake operator's office assignments: assigning and unassigning, the admin lists,
/// the operator's own offices and per-office metrics, and who is behind an impersonation.
/// </summary>
/// <remarks>
/// <para>
/// The service sat at 181 of 212 lines uncovered; only its permission gate was tested.
/// </para>
/// <para>
/// This service crosses offices on purpose, so the per-office metric is the guarantee that
/// matters most. The seeded data already holds the decoy it needs: office A has one pending
/// appointment and office B has none (its appointment is approved). An operator assigned to
/// both must see 1 and 0. A count that ignored the office filter would report 1 for both.
/// </para>
/// <para>
/// The per-office shadow-user provisioner writes to each office's own database, which this base
/// does not have. It is replaced with a substitute and its calls are asserted.
/// </para>
/// <para>All names and emails below are synthetic.</para>
/// </remarks>
public abstract class IntakeAssignmentsAppServiceTests<TStartupModule>
    : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private IIntakeShadowUserProvisioner _shadows = null!;

    private readonly IIntakeAssignmentsAppService _service;
    private readonly IdentityUserManager _users;
    private readonly IdentityRoleManager _roles;
    private readonly IRepository<IntakeOfficeAssignment, Guid> _assignments;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentPrincipalAccessor _principal;
    private readonly IGuidGenerator _guids;

    protected IntakeAssignmentsAppServiceTests()
    {
        _service = GetRequiredService<IIntakeAssignmentsAppService>();
        _users = GetRequiredService<IdentityUserManager>();
        _roles = GetRequiredService<IdentityRoleManager>();
        _assignments = GetRequiredService<IRepository<IntakeOfficeAssignment, Guid>>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _principal = GetRequiredService<ICurrentPrincipalAccessor>();
        _guids = GetRequiredService<IGuidGenerator>();
    }

    protected override void AfterAddApplication(IServiceCollection services)
    {
        _shadows = Substitute.For<IIntakeShadowUserProvisioner>();
        services.Replace(ServiceDescriptor.Singleton(typeof(IIntakeShadowUserProvisioner), _shadows));
    }

    // ------------------------------------------------------------------ harness

    /// <summary>Creates a host user, in the Intake Staff role when asked.</summary>
    private Task<IdentityUser> HostUserAsync(string userName, string name, string surname, bool intake) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(null))
            {
                var user = new IdentityUser(_guids.Create(), userName, $"{userName}@example.test")
                {
                    Name = name,
                    Surname = surname,
                };
                (await _users.CreateAsync(user, "Synthetic-Passw0rd!")).Succeeded.ShouldBeTrue();
                if (intake)
                {
                    if (await _roles.FindByNameAsync(InternalUserRoleDataSeedContributor.IntakeStaffRoleName) == null)
                    {
                        (await _roles.CreateAsync(new IdentityRole(
                            _guids.Create(), InternalUserRoleDataSeedContributor.IntakeStaffRoleName))).Succeeded.ShouldBeTrue();
                    }

                    (await _users.AddToRoleAsync(user, InternalUserRoleDataSeedContributor.IntakeStaffRoleName))
                        .Succeeded.ShouldBeTrue();
                }

                return user;
            }
        });

    private Task<IdentityUser> OperatorAsync() => HostUserAsync("ivy.intake", "Ivy", "Example", intake: true);

    private Task AssignAsync(Guid operatorId, Guid officeId) =>
        _service.AssignAsync(new AssignIntakeOfficeDto { OperatorUserId = operatorId, OfficeId = officeId });

    private async Task<T> As<T>(Guid userId, Func<Task<T>> call, Guid? impersonator = null)
    {
        var claims = new List<Claim> { new(AbpClaimTypes.UserId, userId.ToString()) };
        if (impersonator.HasValue)
        {
            claims.Add(new Claim(AbpClaimTypes.ImpersonatorUserId, impersonator.Value.ToString()));
        }

        using (_principal.Change(new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"))))
        {
            return await call();
        }
    }

    // ------------------------------------------------------------------ assigning

    [Fact]
    public async Task Assigning_records_the_office_once_and_provisions_the_shadow_user_each_time()
    {
        var op = await OperatorAsync();

        await AssignAsync(op.Id, TenantsTestData.TenantARef);
        await AssignAsync(op.Id, TenantsTestData.TenantARef);

        var rows = await WithUnitOfWorkAsync(() => _assignments.GetListAsync(a => a.OperatorUserId == op.Id));
        rows.ShouldHaveSingleItem().OfficeId.ShouldBe(TenantsTestData.TenantARef);
        await _shadows.Received(2).EnsureShadowUserAsync(
            TenantsTestData.TenantARef, op.Id, InternalUserRoleDataSeedContributor.IntakeStaffRoleName);
    }

    [Fact]
    public async Task Only_an_existing_host_intake_operator_can_be_assigned_to_an_existing_office()
    {
        var op = await OperatorAsync();
        var notIntake = await HostUserAsync("rae.staff", "Rae", "Example", intake: false);

        (await Should.ThrowAsync<BusinessException>(() => AssignAsync(Guid.NewGuid(), TenantsTestData.TenantARef)))
            .Code.ShouldBe(CaseEvaluationDomainErrorCodes.InternalUserNotFound);
        (await Should.ThrowAsync<BusinessException>(() => AssignAsync(notIntake.Id, TenantsTestData.TenantARef)))
            .Code.ShouldBe(CaseEvaluationDomainErrorCodes.InternalUserInvalidRole);
        await Should.ThrowAsync<EntityNotFoundException>(() => AssignAsync(op.Id, Guid.NewGuid()));

        (await WithUnitOfWorkAsync(() => _assignments.GetListAsync())).ShouldBeEmpty();
        await _shadows.DidNotReceiveWithAnyArgs().EnsureShadowUserAsync(default, default, default!);
    }

    [Fact]
    public async Task Unassigning_removes_the_office_and_disables_the_shadow_user_even_when_already_gone()
    {
        var op = await OperatorAsync();
        await AssignAsync(op.Id, TenantsTestData.TenantARef);

        await _service.UnassignAsync(op.Id, TenantsTestData.TenantARef);
        await _service.UnassignAsync(op.Id, TenantsTestData.TenantBRef);

        (await WithUnitOfWorkAsync(() => _assignments.GetListAsync())).ShouldBeEmpty();
        await _shadows.Received(1).DisableShadowUserAsync(TenantsTestData.TenantARef, op.Id);
        await _shadows.Received(1).DisableShadowUserAsync(TenantsTestData.TenantBRef, op.Id);
    }

    // ------------------------------------------------------------------ admin lists

    [Fact]
    public async Task The_assignment_lists_name_the_operator_and_the_office()
    {
        var op = await OperatorAsync();
        await AssignAsync(op.Id, TenantsTestData.TenantARef);
        await AssignAsync(op.Id, TenantsTestData.TenantBRef);

        var list = await _service.GetListAsync();
        var paged = await _service.GetPagedListAsync(new GetIntakeAssignmentsInput { MaxResultCount = 10 });

        foreach (var rows in new[] { list.Items, paged.Items })
        {
            rows.Select(r => r.OfficeName).ShouldBe(new[] { TenantsTestData.TenantAName, TenantsTestData.TenantBName });
            rows.ShouldAllBe(r => r.OperatorName == "Ivy Example" && r.OperatorEmail == "ivy.intake@example.test");
        }

        paged.TotalCount.ShouldBe(2);
    }

    [Fact]
    public async Task The_paged_list_filters_sorts_and_pages()
    {
        var ivy = await OperatorAsync();
        var ada = await HostUserAsync("ada.intake", "Ada", "Example", intake: true);
        await AssignAsync(ivy.Id, TenantsTestData.TenantARef);
        await AssignAsync(ada.Id, TenantsTestData.TenantBRef);

        var byOffice = await _service.GetPagedListAsync(new GetIntakeAssignmentsInput { Filter = " tenant-b ", MaxResultCount = 10 });
        var byEmailDesc = await _service.GetPagedListAsync(new GetIntakeAssignmentsInput { Sorting = "operatorEmail desc", MaxResultCount = 10 });
        var byOfficeDesc = await _service.GetPagedListAsync(new GetIntakeAssignmentsInput { Sorting = "office desc", MaxResultCount = 10 });
        var byNameDesc = await _service.GetPagedListAsync(new GetIntakeAssignmentsInput { Sorting = "operatorName DESC", MaxResultCount = 10 });
        var secondPage = await _service.GetPagedListAsync(new GetIntakeAssignmentsInput { SkipCount = 1, MaxResultCount = 1 });

        byOffice.Items.ShouldHaveSingleItem().OperatorName.ShouldBe("Ada Example");
        byEmailDesc.Items.Select(r => r.OperatorEmail).ShouldBe(new[] { "ivy.intake@example.test", "ada.intake@example.test" });
        byOfficeDesc.Items.Select(r => r.OfficeName).ShouldBe(new[] { TenantsTestData.TenantBName, TenantsTestData.TenantAName });
        byNameDesc.Items.Select(r => r.OperatorName).ShouldBe(new[] { "Ivy Example", "Ada Example" });
        secondPage.TotalCount.ShouldBe(2);
        secondPage.Items.ShouldHaveSingleItem().OperatorName.ShouldBe("Ivy Example");
    }

    [Fact]
    public async Task The_pickers_offer_host_intake_operators_and_every_office()
    {
        await OperatorAsync();
        await HostUserAsync("rae.staff", "Rae", "Example", intake: false);

        var operators = await _service.GetAssignableOperatorsAsync();
        var offices = await _service.GetOfficeOptionsAsync();

        operators.Items.ShouldHaveSingleItem().DisplayName.ShouldBe("Ivy Example (ivy.intake@example.test)");
        offices.Items.Select(o => o.DisplayName).ShouldBe(new[] { TenantsTestData.TenantAName, TenantsTestData.TenantBName });
    }

    // ------------------------------------------------------------------ the operator's view

    [Fact]
    public async Task An_operator_sees_each_assigned_office_counted_inside_that_office_only()
    {
        var op = await OperatorAsync();
        await AssignAsync(op.Id, TenantsTestData.TenantARef);
        await AssignAsync(op.Id, TenantsTestData.TenantBRef);

        var offices = await As(op.Id, () => _service.GetMyOfficesAsync());
        var metrics = await As(op.Id, () => _service.GetMyOfficeMetricsAsync());

        offices.Items.Select(o => o.Id).ShouldBe(new[] { TenantsTestData.TenantARef, TenantsTestData.TenantBRef });
        metrics.Items.Select(m => (m.OfficeName, m.PendingRequests))
            .ShouldBe(new[] { (TenantsTestData.TenantAName, 1), (TenantsTestData.TenantBName, 0) });
        metrics.Items.ShouldAllBe(m => m.PendingChangeRequests == 0);
    }

    [Fact]
    public async Task An_anonymous_caller_has_no_offices_and_no_metrics()
    {
        (await _service.GetMyOfficesAsync()).Items.ShouldBeEmpty();
        (await _service.GetMyOfficeMetricsAsync()).Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task While_impersonating_the_operator_can_switch_between_their_offices_and_is_named()
    {
        var op = await OperatorAsync();
        await AssignAsync(op.Id, TenantsTestData.TenantARef);
        var shadowId = Guid.NewGuid();

        var switchable = await As(shadowId, () => _service.GetSwitchableOfficesAsync(), impersonator: op.Id);
        var info = await As(shadowId, () => _service.GetImpersonatorInfoAsync(), impersonator: op.Id);

        switchable.Items.ShouldHaveSingleItem().Id.ShouldBe(TenantsTestData.TenantARef);
        info.IsImpersonating.ShouldBeTrue();
        info.Name.ShouldBe("Ivy Example");
        info.Roles.ShouldBe(new[] { InternalUserRoleDataSeedContributor.IntakeStaffRoleName });
    }

    [Fact]
    public async Task Without_an_impersonation_there_is_nothing_to_switch_to_and_nobody_behind_it()
    {
        var plain = await As(Guid.NewGuid(), () => _service.GetImpersonatorInfoAsync());
        var unknown = await As(Guid.NewGuid(), () => _service.GetImpersonatorInfoAsync(), impersonator: Guid.NewGuid());
        var switchable = await As(Guid.NewGuid(), () => _service.GetSwitchableOfficesAsync());

        plain.IsImpersonating.ShouldBeFalse();
        unknown.IsImpersonating.ShouldBeFalse();
        switchable.Items.ShouldBeEmpty();
    }
}
