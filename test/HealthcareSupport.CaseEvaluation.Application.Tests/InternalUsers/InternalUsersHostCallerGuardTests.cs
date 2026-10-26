using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Identity;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.InternalUsers;

/// <summary>
/// The host-caller guard on the four internal-users methods, tested with no permission in the way.
///
/// <para>WHY THIS HARNESS. It allows every permission, so a refusal here can only come from the guard
/// inside the method. Through the real authorization pipeline the Host-only permissions already stop
/// an office caller (<c>InternalUsersHostOnlyGateTests</c>,
/// <c>InternalUsersHostOnlyAuthorizationTests</c>), so the guard never runs there; this is the
/// situation it exists for: if a permission's side is ever widened, the guard still refuses before
/// any user is created, listed, mailed or any office is listed.</para>
///
/// <para>Each refusal has a positive control with no office in scope, which shows the same call does
/// reach the method body, so a refusal cannot be passing because the call never got there. All names
/// and emails are synthetic.</para>
/// </summary>
/// <typeparam name="TStartupModule">The test startup module that supplies the database.</typeparam>
public abstract class InternalUsersHostCallerGuardTests<TStartupModule>
    : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private const string IntakeStaff = "Intake Staff";

    private static readonly Guid Office = TenantsTestData.TenantARef;

    private readonly IInternalUsersAppService _appService;
    private readonly IdentityUserManager _userManager;
    private readonly ICurrentTenant _currentTenant;

    protected InternalUsersHostCallerGuardTests()
    {
        _appService = GetRequiredService<IInternalUsersAppService>();
        _userManager = GetRequiredService<IdentityUserManager>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    // ---- Inside an office: refused before any work ----

    [Fact]
    public async Task Create_InsideAnOffice_IsRefused_AndNoAccountIsCreated()
    {
        var email = NewEmail();

        await ShouldBeRefusedInsideAnOfficeAsync(() => _appService.CreateAsync(Operator(email)));

        (await FindHostUserAsync(email)).ShouldBeNull();
    }

    [Fact]
    public async Task GetInternalUsers_InsideAnOffice_IsRefused()
    {
        await ShouldBeRefusedInsideAnOfficeAsync(
            () => _appService.GetInternalUsersAsync(new GetInternalUsersInput { MaxResultCount = 10 }));
    }

    [Fact]
    public async Task SendPasswordResetEmail_InsideAnOffice_IsRefused()
    {
        // An unknown id: at the host this reaches the lookup and fails as not found (below), so a
        // refusal here can only be the guard.
        await ShouldBeRefusedInsideAnOfficeAsync(() => _appService.SendPasswordResetEmailAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task ChangeRole_InsideAnOffice_IsRefused()
    {
        // An unknown id: at the host this reaches the lookup and fails as not found (below).
        await ShouldBeRefusedInsideAnOfficeAsync(() => _appService.ChangeRoleAsync(
            Guid.NewGuid(), new ChangeInternalUserRoleDto { RoleName = "Staff Supervisor" }));
    }

    [Fact]
    public async Task GetTenantOptions_InsideAnOffice_IsRefused()
    {
        await ShouldBeRefusedInsideAnOfficeAsync(() => _appService.GetTenantOptionsAsync(null));
    }

    // ---- At the host: the same calls reach the method body ----

    [Fact]
    public async Task Create_AtTheHost_ReachesTheBody()
    {
        // A role outside the creatable set: the body refuses it as a business rule, not on
        // authorization, and stops before any write.
        var input = Operator(NewEmail());
        input.RoleName = "TEST-not-a-creatable-role";

        await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(() => _appService.CreateAsync(input)));
    }

    [Fact]
    public async Task GetInternalUsers_AtTheHost_ReachesTheBody()
    {
        var result = await WithUnitOfWorkAsync(
            () => _appService.GetInternalUsersAsync(new GetInternalUsersInput { MaxResultCount = 10 }));

        result.ShouldNotBeNull();
    }

    [Fact]
    public async Task SendPasswordResetEmail_AtTheHost_ReachesTheBody()
    {
        await Should.ThrowAsync<BusinessException>(
            () => WithUnitOfWorkAsync(() => _appService.SendPasswordResetEmailAsync(Guid.NewGuid())));
    }

    [Fact]
    public async Task ChangeRole_AtTheHost_ReachesTheBody()
    {
        (await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(() => _appService.ChangeRoleAsync(
            Guid.NewGuid(), new ChangeInternalUserRoleDto { RoleName = "Staff Supervisor" }))))
            .Code.ShouldBe(CaseEvaluationDomainErrorCodes.InternalUserNotFound);
    }

    [Fact]
    public async Task GetTenantOptions_AtTheHost_ReachesTheBody()
    {
        var offices = await WithUnitOfWorkAsync(() => _appService.GetTenantOptionsAsync(null));

        offices.Items.ShouldContain(o => o.Id == Office);
    }

    // ------------------------------------------------------------------------

    private async Task ShouldBeRefusedInsideAnOfficeAsync(Func<Task> call)
    {
        using (_currentTenant.Change(Office))
        {
            var refusal = await Should.ThrowAsync<AbpAuthorizationException>(() => WithUnitOfWorkAsync(call));
            refusal.Message.ShouldContain("managed from the host");
        }
    }

    private static string NewEmail() => $"test-host-guard.{Guid.NewGuid().ToString("N")[..8]}@example.test";

    private static CreateInternalUserDto Operator(string email) => new()
    {
        Email = email,
        FirstName = "Synthetic",
        LastName = "Operator",
        RoleName = IntakeStaff,
    };

    private Task<IdentityUser?> FindHostUserAsync(string email) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(null))
            {
                return await _userManager.FindByEmailAsync(email);
            }
        });
}
