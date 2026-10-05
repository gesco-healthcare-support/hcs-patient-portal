using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Shouldly;
using Volo.Abp.Identity;
using Volo.Abp.Modularity;
using Xunit;
using IdentityUser = Volo.Abp.Identity.IdentityUser;

namespace HealthcareSupport.CaseEvaluation.AuthServer.Tests;

/// <summary>
/// Guards the shared harness's "Default" identity token provider (#1261). It used to validate
/// ANY token, so no test under the harness could show a token being refused. These pin that it now
/// accepts only what it issued, for that user and purpose, and rejects it once consumed.
/// </summary>
public abstract class HarnessTokenProviderTests<TStartupModule>
    : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private const string Purpose = "EmailConfirmation";
    private readonly IdentityUserManager _userManager;

    protected HarnessTokenProviderTests()
    {
        _userManager = GetRequiredService<IdentityUserManager>();
    }

    private async Task<Guid> CreateUserAsync()
    {
        var email = $"tok-{Guid.NewGuid():N}@example.test";
        var user = new IdentityUser(Guid.NewGuid(), email, email, tenantId: null);
        await WithUnitOfWorkAsync(async () => (await _userManager.CreateAsync(user, "Synthetic-Pass1!")).Succeeded.ShouldBeTrue());
        return user.Id;
    }

    private async Task<bool> ValidateAsync(Guid id, string purpose, string token)
    {
        var result = false;
        await WithUnitOfWorkAsync(async () =>
        {
            var user = await _userManager.GetByIdAsync(id);
            result = await _userManager.VerifyUserTokenAsync(user, TokenOptions.DefaultProvider, purpose, token);
        });
        return result;
    }

    private async Task<string> IssueAsync(Guid id, string purpose)
    {
        var token = "";
        await WithUnitOfWorkAsync(async () =>
        {
            var user = await _userManager.GetByIdAsync(id);
            token = await _userManager.GenerateUserTokenAsync(user, TokenOptions.DefaultProvider, purpose);
        });
        return token;
    }

    [Fact]
    public async Task A_token_the_harness_issued_validates()
    {
        var id = await CreateUserAsync();
        var token = await IssueAsync(id, Purpose);
        (await ValidateAsync(id, Purpose, token)).ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("garbage")]
    [InlineData("test-token")]
    public async Task A_garbage_or_blank_token_is_refused(string token)
    {
        var id = await CreateUserAsync();
        (await ValidateAsync(id, Purpose, token)).ShouldBeFalse();
    }

    [Fact]
    public async Task A_tampered_token_is_refused()
    {
        var id = await CreateUserAsync();
        var token = await IssueAsync(id, Purpose);
        (await ValidateAsync(id, Purpose, token + "x")).ShouldBeFalse();
    }

    [Fact]
    public async Task A_token_for_another_purpose_is_refused()
    {
        var id = await CreateUserAsync();
        var token = await IssueAsync(id, Purpose);
        (await ValidateAsync(id, "ResetPassword", token)).ShouldBeFalse();
    }

    [Fact]
    public async Task A_token_for_another_user_is_refused()
    {
        var a = await CreateUserAsync();
        var b = await CreateUserAsync();
        var token = await IssueAsync(a, Purpose);
        (await ValidateAsync(b, Purpose, token)).ShouldBeFalse();
    }

    [Fact]
    public async Task A_consumed_token_is_refused()
    {
        var id = await CreateUserAsync();
        var token = await IssueAsync(id, Purpose);
        (await ValidateAsync(id, Purpose, token)).ShouldBeTrue();
        // Consuming a token rotates the security stamp, exactly as a password reset does.
        await WithUnitOfWorkAsync(async () =>
            (await _userManager.UpdateSecurityStampAsync(await _userManager.GetByIdAsync(id))).Succeeded.ShouldBeTrue());
        (await ValidateAsync(id, Purpose, token)).ShouldBeFalse();
    }
}
