using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Notifications;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using HealthcareSupport.CaseEvaluation.TestData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Identity;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.InternalUsers;

/// <summary>
/// The branches of <see cref="InternalUsersAppService"/> that <c>EfCoreInternalUsersAppServiceTests</c>
/// does not reach: a creatable role missing from the database, an identity refusal during create,
/// a welcome email that fails, the unfiltered list, and sorting by role and by status.
/// </summary>
/// <remarks>
/// The notification dispatcher is replaced for THIS class only, in <c>AfterAddApplication</c>, so
/// the welcome email can fail with an ordinary exception. All users are host operators created per
/// test, with synthetic names and emails.
/// </remarks>
public abstract class InternalUsersFailurePathTests<TStartupModule>
    : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private const string IntakeStaff = "Intake Staff";
    private const string StaffSupervisor = "Staff Supervisor";

    private INotificationDispatcher _dispatcher = null!;

    private readonly IInternalUsersAppService _service;
    private readonly IdentityUserManager _userManager;
    private readonly IdentityRoleManager _roleManager;
    private readonly IIdentityRoleRepository _roleRepository;
    private readonly ICurrentTenant _currentTenant;

    protected InternalUsersFailurePathTests()
    {
        _service = GetRequiredService<IInternalUsersAppService>();
        _userManager = GetRequiredService<IdentityUserManager>();
        _roleManager = GetRequiredService<IdentityRoleManager>();
        _roleRepository = GetRequiredService<IIdentityRoleRepository>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    protected override void AfterAddApplication(IServiceCollection services)
    {
        _dispatcher = Substitute.For<INotificationDispatcher>();
        services.Replace(ServiceDescriptor.Singleton(typeof(INotificationDispatcher), _dispatcher));
    }

    // ------------------------------------------------------------------ harness

    private static string NewToken() => Guid.NewGuid().ToString("N")[..8];

    private static CreateInternalUserDto Operator(string email, string role = IntakeStaff) => new()
    {
        Email = email,
        FirstName = "Synthetic",
        LastName = "Operator",
        RoleName = role,
    };

    private Task<InternalUserCreatedDto> CreateAsync(CreateInternalUserDto input) =>
        WithUnitOfWorkAsync(() => _service.CreateAsync(input));

    private Task<IdentityUser?> FindByEmailAsync(string email)
    {
        using (_currentTenant.Change(null))
        {
            return WithUnitOfWorkAsync(() => _userManager.FindByEmailAsync(email));
        }
    }

    private async Task SeedOperatorAsync(string token, string suffix, string role, bool isActive)
    {
        using (_currentTenant.Change(null))
        {
            await WithUnitOfWorkAsync(async () =>
            {
                var user = new IdentityUser(Guid.NewGuid(), $"op-{token}-{suffix}", $"op-{token}-{suffix}@example.test", tenantId: null)
                {
                    Name = "Synthetic",
                    Surname = $"Operator-{suffix}",
                };
                user.SetIsActive(isActive);
                (await _userManager.CreateAsync(user, IdentityUsersTestData.SeedPassword)).Succeeded.ShouldBeTrue();
                (await _userManager.AddToRoleAsync(user, role)).Succeeded.ShouldBeTrue();
            });
        }
    }

    private Task<List<InternalUserListDto>> ListAsync(string? filter, string sorting) =>
        WithUnitOfWorkAsync(async () => (await _service.GetInternalUsersAsync(
            new GetInternalUsersInput { Filter = filter, Sorting = sorting, MaxResultCount = 1000 })).Items.ToList());

    // ------------------------------------------------------------------ create refusals

    [Fact]
    public async Task A_creatable_role_missing_from_the_database_is_refused_as_missing()
    {
        using (_currentTenant.Change(null))
        {
            await WithUnitOfWorkAsync(async () =>
            {
                var role = await _roleManager.FindByNameAsync(StaffSupervisor);
                role.ShouldNotBeNull("the fixture needs the seeded role, so that deleting it is what the test changes");
                await _roleRepository.DeleteAsync(role, autoSave: true);
            });
        }
        var email = $"op-{NewToken()}@example.test";

        var refused = await Should.ThrowAsync<BusinessException>(() => CreateAsync(Operator(email, StaffSupervisor)));

        refused.Code.ShouldBe(CaseEvaluationDomainErrorCodes.InternalUserRoleMissing);
        (await FindByEmailAsync(email)).ShouldBeNull();
    }

    [Fact]
    public async Task An_email_the_identity_rules_refuse_as_a_user_name_fails_the_create_and_leaves_no_account()
    {
        var email = $"o'op-{NewToken()}@example.test";

        var refused = await Should.ThrowAsync<BusinessException>(() => CreateAsync(Operator(email)));

        refused.Code.ShouldBe(CaseEvaluationDomainErrorCodes.InternalUserCreateFailed);
        ((string)refused.Data["Errors"]!).ShouldNotBeNullOrWhiteSpace();
        (await FindByEmailAsync(email)).ShouldBeNull();
        _dispatcher.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task A_failed_welcome_email_leaves_the_account_created_and_says_so()
    {
        _dispatcher.DispatchAsync(
                NotificationTemplateConsts.Codes.InternalUserCreated, Arg.Any<IReadOnlyCollection<NotificationRecipient>>(),
                Arg.Any<IReadOnlyDictionary<string, object?>>(), Arg.Any<string>(),
                Arg.Any<PacketAttachmentRef?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Synthetic mail outage"));
        var email = $"op-{NewToken()}@example.test";

        var created = await CreateAsync(Operator(email));

        created.WelcomeEmailQueued.ShouldBeFalse();
        var stored = await FindByEmailAsync(email);
        stored.ShouldNotBeNull();
        stored.Id.ShouldBe(created.UserId);
    }

    // ------------------------------------------------------------------ list

    [Fact]
    public async Task With_no_filter_the_list_holds_every_internal_operator()
    {
        var token = NewToken();
        await SeedOperatorAsync(token, "a", IntakeStaff, isActive: true);
        await SeedOperatorAsync(token, "b", StaffSupervisor, isActive: true);

        var rows = await ListAsync(filter: null, sorting: "email asc");

        rows.Count(r => r.Email.Contains(token)).ShouldBe(2);
    }

    [Fact]
    public async Task The_list_sorts_by_role_and_by_status_in_either_direction()
    {
        var token = NewToken();
        await SeedOperatorAsync(token, "a", StaffSupervisor, isActive: true);
        await SeedOperatorAsync(token, "b", IntakeStaff, isActive: false);

        (await ListAsync(token, "role asc")).Select(r => r.Role).ShouldBe(new[] { IntakeStaff, StaffSupervisor });
        (await ListAsync(token, "role desc")).Select(r => r.Role).ShouldBe(new[] { StaffSupervisor, IntakeStaff });
        (await ListAsync(token, "isactive asc")).Select(r => r.IsActive).ShouldBe(new[] { false, true });
        (await ListAsync(token, "status desc")).Select(r => r.IsActive).ShouldBe(new[] { true, false });
    }
}
