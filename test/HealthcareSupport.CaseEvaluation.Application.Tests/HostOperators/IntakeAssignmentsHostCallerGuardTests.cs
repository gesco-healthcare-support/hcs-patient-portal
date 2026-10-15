using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Identity;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.HostOperators;

/// <summary>
/// The host-caller guard on the six intake-assignment management methods, tested with no
/// permission in the way.
///
/// <para>WHY THIS HARNESS. It allows every permission, so a refusal here can only come from the guard
/// inside the method. Through the real authorization pipeline the Host-only permissions already stop
/// an office caller (<c>IntakeAssignmentsHostOnlyAuthorizationTests</c>), so the guard never runs
/// there; this is the situation it exists for: if a permission's side is ever widened, the guard
/// still refuses, before any assignment, operator, office or shadow user is read or written.</para>
///
/// <para>Each refusal has a positive control with no office in scope, which shows the same call does
/// reach the method body, so a refusal cannot be passing because the call never got there. Every test
/// seeds its own operator and assignment.</para>
/// </summary>
/// <typeparam name="TStartupModule">The test startup module that supplies the database.</typeparam>
public abstract class IntakeAssignmentsHostCallerGuardTests<TStartupModule>
    : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private static readonly Guid Office = TenantsTestData.TenantARef;

    private readonly IIntakeAssignmentsAppService _appService;
    private readonly IRepository<IntakeOfficeAssignment, Guid> _assignments;
    private readonly IdentityUserManager _users;
    private readonly ICurrentTenant _currentTenant;

    protected IntakeAssignmentsHostCallerGuardTests()
    {
        _appService = GetRequiredService<IIntakeAssignmentsAppService>();
        _assignments = GetRequiredService<IRepository<IntakeOfficeAssignment, Guid>>();
        _users = GetRequiredService<IdentityUserManager>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    // ---- Inside an office: refused before any work ----

    [Fact]
    public async Task GetList_InsideAnOffice_IsRefused_BeforeAnyWork()
    {
        await SeedAsync();

        await ShouldBeRefusedInsideAnOfficeAsync(() => _appService.GetListAsync());
    }

    [Fact]
    public async Task GetPagedList_InsideAnOffice_IsRefused_BeforeAnyWork()
    {
        var seeded = await SeedAsync();

        await ShouldBeRefusedInsideAnOfficeAsync(
            () => _appService.GetPagedListAsync(new GetIntakeAssignmentsInput { Filter = seeded.Label, MaxResultCount = 10 }));
    }

    [Fact]
    public async Task Assign_InsideAnOffice_IsRefused_BeforeAnyWork()
    {
        var seeded = await SeedAsync();
        var other = await HostIntakeOperatorAsync(seeded.Label + "b");

        await ShouldBeRefusedInsideAnOfficeAsync(
            () => _appService.AssignAsync(new AssignIntakeOfficeDto { OperatorUserId = other.Id, OfficeId = Office }));

        (await AssignmentCountAsync(other.Id)).ShouldBe(0);
    }

    [Fact]
    public async Task Unassign_InsideAnOffice_IsRefused_BeforeAnyWork()
    {
        var seeded = await SeedAsync();

        await ShouldBeRefusedInsideAnOfficeAsync(() => _appService.UnassignAsync(seeded.Operator.Id, Office));

        (await AssignmentCountAsync(seeded.Operator.Id)).ShouldBe(1);
    }

    [Fact]
    public async Task GetAssignableOperators_InsideAnOffice_IsRefused_BeforeAnyWork()
    {
        await SeedAsync();

        await ShouldBeRefusedInsideAnOfficeAsync(() => _appService.GetAssignableOperatorsAsync());
    }

    [Fact]
    public async Task GetOfficeOptions_InsideAnOffice_IsRefused_BeforeAnyWork()
    {
        await SeedAsync();

        await ShouldBeRefusedInsideAnOfficeAsync(() => _appService.GetOfficeOptionsAsync());
    }

    // ---- At the host: the same calls reach the method body ----

    [Fact]
    public async Task GetList_AtTheHost_ReachesTheBody()
    {
        var seeded = await SeedAsync();

        var list = await _appService.GetListAsync();

        list.Items.Where(r => r.OperatorUserId == seeded.Operator.Id && r.OfficeId == Office).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task GetPagedList_AtTheHost_ReachesTheBody()
    {
        var seeded = await SeedAsync();

        var paged = await _appService.GetPagedListAsync(new GetIntakeAssignmentsInput { Filter = seeded.Label, MaxResultCount = 10 });

        paged.Items.Where(r => r.OperatorUserId == seeded.Operator.Id && r.OfficeId == Office).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Assign_AtTheHost_ReachesTheBody()
    {
        await SeedAsync();

        // An unknown operator: the body fails on the lookup, not on authorization.
        await Should.ThrowAsync<BusinessException>(
            () => _appService.AssignAsync(new AssignIntakeOfficeDto { OperatorUserId = Guid.NewGuid(), OfficeId = Office }));
    }

    [Fact]
    public async Task Unassign_AtTheHost_ReachesTheBody()
    {
        await SeedAsync();

        // An unknown operator: the body finds no row, then fails resolving the operator for the shadow user.
        await Should.ThrowAsync<BusinessException>(() => _appService.UnassignAsync(Guid.NewGuid(), Office));
    }

    [Fact]
    public async Task GetAssignableOperators_AtTheHost_ReachesTheBody()
    {
        var seeded = await SeedAsync();

        var operators = await _appService.GetAssignableOperatorsAsync();

        operators.Items.Where(o => o.Id == seeded.Operator.Id).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task GetOfficeOptions_AtTheHost_ReachesTheBody()
    {
        await SeedAsync();

        var offices = await _appService.GetOfficeOptionsAsync();

        offices.Items.Where(o => o.Id == Office).ShouldHaveSingleItem();
    }

    // ------------------------------------------------------------------------

    private async Task ShouldBeRefusedInsideAnOfficeAsync(Func<Task> call)
    {
        using (_currentTenant.Change(Office))
        {
            await Should.ThrowAsync<AbpAuthorizationException>(call);
        }
    }

    private Task<int> AssignmentCountAsync(Guid operatorId) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(null))
            {
                return await _assignments.CountAsync(a => a.OperatorUserId == operatorId);
            }
        });

    /// <summary>A host Intake operator, assigned to <see cref="Office"/>, under a fresh label.</summary>
    private async Task<Seeded> SeedAsync()
    {
        var label = Guid.NewGuid().ToString("N")[..8];
        var op = await HostIntakeOperatorAsync(label);

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(null))
            {
                await _assignments.InsertAsync(new IntakeOfficeAssignment(Guid.NewGuid(), op.Id, Office), autoSave: true);
            }
        });

        return new Seeded(label, op);
    }

    private Task<IdentityUser> HostIntakeOperatorAsync(string label) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(null))
            {
                var email = $"test-intake-guard.{label}@example.test";
                var user = new IdentityUser(Guid.NewGuid(), email, email, tenantId: null) { Name = "TEST-intake", Surname = label };
                (await _users.CreateAsync(user, "Synthetic-Passw0rd!")).Succeeded.ShouldBeTrue();
                (await _users.AddToRoleAsync(user, InternalUserRoleDataSeedContributor.IntakeStaffRoleName)).Succeeded.ShouldBeTrue();
                return user;
            }
        });

    private sealed record Seeded(string Label, IdentityUser Operator);
}
