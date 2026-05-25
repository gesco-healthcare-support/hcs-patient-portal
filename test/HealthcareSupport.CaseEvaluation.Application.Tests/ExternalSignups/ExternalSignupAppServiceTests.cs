using System;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Modularity;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.ExternalSignups;

/// <summary>
/// BUG-012 Sub-bug 1 (2026-05-22) -- DI-resolved integration tests for
/// <see cref="IExternalSignupAppService"/>. The pure-helper tests in
/// <see cref="ExternalSignupValidatorUnitTests"/> exercise the static
/// validator directly; this file exercises the full AppService instance
/// resolved through ABP's DI graph so the constructor's
/// <c>IStringLocalizer&lt;CaseEvaluationResource&gt;</c> wiring + the
/// caller-line <c>ValidateRegistrationInput(input, _localizer)</c>
/// invocation get coverage credit.
///
/// <para>The test deliberately submits a DTO that fails validation at
/// the first gate (Password/ConfirmPassword mismatch). This proves the
/// AppService is constructible via DI and that <c>RegisterAsync</c>
/// reaches the validator before any expensive DB / tenant / email
/// work -- a single short-circuit covers all the constructor +
/// caller lines without standing up tenant context.</para>
/// </summary>
public abstract class ExternalSignupAppServiceTests<TStartupModule>
    : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IExternalSignupAppService _appService;

    protected ExternalSignupAppServiceTests()
    {
        _appService = GetRequiredService<IExternalSignupAppService>();
    }

    [Fact]
    public async Task RegisterAsync_PasswordMismatch_ThrowsLocalizedConfirmPasswordMismatch()
    {
        // Building a valid-shape DTO that breaks the FIRST validator gate
        // (password mismatch). Anything reaching downstream (tenant lookup,
        // email check, role assignment) would have side effects; the
        // validator must throw before that. The throw + the caller's pass
        // of `_localizer` is what we are covering here.
        var dto = new ExternalUserSignUpDto
        {
            UserType = ExternalUserType.Patient,
            Email = "bug012-sub1-di@example.test",
            Password = "Test1234!",
            ConfirmPassword = "Mismatch99!",
            FirstName = "Sub1",
            LastName = "DI",
            TenantId = Guid.NewGuid(),
        };

        var ex = await Should.ThrowAsync<UserFriendlyException>(
            () => _appService.RegisterAsync(dto));

        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.RegistrationConfirmPasswordMismatch);
        // The localized message reached the consumer via the DI'd
        // IStringLocalizer -- the proof that line 489's
        // ValidateRegistrationInput(input, _localizer) call wired
        // through correctly.
        ex.Message.ShouldBe("Password and confirm password do not match.");
    }
}
