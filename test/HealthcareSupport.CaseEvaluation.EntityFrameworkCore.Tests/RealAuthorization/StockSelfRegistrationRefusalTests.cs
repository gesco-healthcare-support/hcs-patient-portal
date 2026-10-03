using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.ExternalSignups;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Account;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.RealAuthorization;

/// <summary>
/// Pins <see cref="StockSelfRegistrationRefusal"/>: ABP's stock self-registration is refused,
/// AND the portal's own sign-up still registers. Both halves are the deliverable -- a refusal
/// that also stopped the portal sign-up would pass the first test and break every new user.
///
/// <para>The stock path is exercised through <see cref="IAccountAppService.RegisterAsync"/>, the
/// method both stock write paths call: <c>POST api/account/register</c> and the POST handler of
/// the stock <c>/Account/Register</c> page. That the page's handler calls this method is how ABP's
/// open-source Account module is built; the Pro page is obfuscated and that was not run.</para>
/// </summary>
[Collection(RealAuthorizationCollection.Name)]
public class StockSelfRegistrationRefusalTests : CaseEvaluationRealAuthorizationTestBase
{
    private const string Password = "TEST-Pw1!aaaa";

    private readonly ICurrentTenant _currentTenant;

    public StockSelfRegistrationRefusalTests()
    {
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    [Fact]
    public async Task StockRegistration_IsRefused_AndCreatesNoAccount()
    {
        var fixture = await GetFixtureAsync();
        var email = NewEmail("stock");
        Exception? caught = null;

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(fixture.Office.OfficeId))
            {
                try
                {
                    await GetRequiredService<IAccountAppService>().RegisterAsync(new RegisterDto
                    {
                        AppName = "Angular",
                        UserName = email,
                        EmailAddress = email,
                        Password = Password,
                    });
                }
                catch (Exception ex)
                {
                    caught = ex;
                }
            }
        }, requiresNew: true);

        caught.ShouldBeOfType<UserFriendlyException>(
            "ABP's stock registration creates an account with no role; it must be refused.")
            .Message.ShouldBe(StockSelfRegistrationRefusal.RefusalMessage);
        (await FindUserAsync(fixture, email)).ShouldBeNull(
            "the refusal must stop the account being created, not merely report an error after it was.");
    }

    [Fact]
    public async Task PortalSignUp_StillRegisters_WithTheRoleSignedUpFor()
    {
        var fixture = await GetFixtureAsync();
        var email = NewEmail("portal");

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(fixture.Office.OfficeId))
            {
                await GetRequiredService<IExternalSignupAppService>().RegisterAsync(new ExternalUserSignUpDto
                {
                    UserType = ExternalUserType.Patient,
                    Email = email,
                    Password = Password,
                    ConfirmPassword = Password,
                    TenantId = fixture.Office.OfficeId,
                });
            }
        }, requiresNew: true);

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(fixture.Office.OfficeId))
            {
                var userManager = GetRequiredService<IdentityUserManager>();
                var user = await userManager.FindByEmailAsync(email);
                user.ShouldNotBeNull("the portal sign-up must keep working; it is how every external user joins.");
                (await userManager.IsInRoleAsync(user!, "Patient")).ShouldBeTrue();
            }
        }, requiresNew: true);
    }

    [Fact]
    public void Refusal_AttachesToTheAccountService_AndNotToThePortalSignUp()
    {
        StockSelfRegistrationRefusal.AppliesTo(typeof(AccountAppService)).ShouldBeTrue();
        StockSelfRegistrationRefusal.AppliesTo(typeof(ExternalSignupAppService)).ShouldBeFalse(
            "the portal sign-up's method is also named RegisterAsync; attaching here would refuse it too.");
    }

    private static string NewEmail(string label) =>
        $"TEST-{label}-{Guid.NewGuid():N}"[..24] + "@example.test";

    private async Task<IdentityUser?> FindUserAsync(AuthorizationFixture fixture, string email)
    {
        IdentityUser? user = null;
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(fixture.Office.OfficeId))
            {
                user = await GetRequiredService<IdentityUserManager>().FindByEmailAsync(email);
            }
        }, requiresNew: true);
        return user;
    }
}
