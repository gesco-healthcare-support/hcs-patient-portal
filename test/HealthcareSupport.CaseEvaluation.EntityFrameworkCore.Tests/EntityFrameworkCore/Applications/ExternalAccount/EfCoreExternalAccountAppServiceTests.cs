using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Validation;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.ExternalAccount;

/// <summary>
/// Integration coverage for the password-reset seam of
/// <see cref="ExternalAccountAppService"/> (phase 8 tranche 1, item 6).
///
/// <para>WHY THIS CLASS EXISTS RATHER THAN MORE FACTS IN
/// <c>ExternalAccountAppServiceUnitTests</c>. That file is a plain class with no base and no DI.
/// It reaches only the <c>internal static</c> helpers through <c>InternalsVisibleTo</c>, and
/// every uncovered member here is an instance method behind five injected dependencies -- two of
/// them (<c>IsRateLimitedAsync</c>, <c>StampRateLimitAsync</c>) private and reachable only
/// through the public entry points. No number of Facts added there could touch this code.</para>
///
/// <para>Its docstring blamed "a pre-existing Phase 4 license-checker test-host crash" for
/// bypassing the ABP harness and promised the integration assertions in "a follow-up integration
/// suite gated on the test-host crash being resolved". That was 2026-05-03. The gate is long
/// gone -- 32 files in Application.Tests drive the harness today -- and this is that follow-up
/// suite, arriving four months late.</para>
///
/// <para>THE RATE-LIMIT FACTS RUN AGAINST A REAL <c>IDistributedCache</c>, and that was
/// established by experiment rather than by reading the claim at
/// <c>ExternalAccountAppService.cs:464</c>. Grepping the test projects for
/// <c>AddDistributedMemoryCache</c> / <c>MemoryDistributedCache</c> returns nothing, which proves
/// only that they register no cache EXPLICITLY -- ABP's own module chain supplies one. It was
/// settled by breaking the write-then-read round trip (stamping under a key the read cannot find)
/// and confirming these Facts then FAIL. A stamp-then-assert that a no-op cache would satisfy
/// equally would be proving nothing.</para>
///
/// <para>NO MAIL CAN LEAVE. Every address below is unregistered, so each flow returns before any
/// template lookup or dispatch.</para>
/// </summary>
[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class EfCoreExternalAccountAppServiceTests
    : CaseEvaluationApplicationTestBase<CaseEvaluationEntityFrameworkCoreTestModule>
{
    private readonly IExternalAccountAppService _externalAccountAppService;

    public EfCoreExternalAccountAppServiceTests()
    {
        _externalAccountAppService = GetRequiredService<IExternalAccountAppService>();
    }

    /// <summary>A fresh unregistered address, so no two Facts share a rate-limit key.</summary>
    private static string NewUnregisteredEmail(string label) =>
        $"TEST-{label}-{Guid.NewGuid():N}@test.local";

    // ------------------------------------------------------------------------
    // SendPasswordResetCodeAsync -- the throttle.
    //
    // Item D (2026-08-22) added this because the reset flow was COMPLETELY UNTHROTTLED for real
    // users: the only limiter was ASP.NET middleware keyed on a controller path that nothing
    // called, while the AuthServer page users actually reach invokes this service in-process
    // through DI, with no HTTP hop and therefore no middleware. An anonymous form could send
    // unlimited real mail through the SMTP relay.
    // ------------------------------------------------------------------------

    [Fact]
    public async Task SendPasswordResetCodeAsync_WhenCalledAgainInsideTheCooldown_IsThrottled()
    {
        var email = NewUnregisteredEmail("reset-throttle");

        // The first call stamps. It must NOT throw -- an unregistered address is a silent success
        // by design, so a throw here would mean the second call's throw is not the limiter's.
        await Should.NotThrowAsync(
            async () => await _externalAccountAppService.SendPasswordResetCodeAsync(
                new SendPasswordResetCodeInput { Email = email }));

        var ex = await Should.ThrowAsync<BusinessException>(
            async () => await _externalAccountAppService.SendPasswordResetCodeAsync(
                new SendPasswordResetCodeInput { Email = email }),
            "A second reset request for the same address inside the 60-second cooldown must be "
            + "refused. If this stops throwing, the throttle added by item D is no longer "
            + "enforcing and an anonymous caller can drive unlimited mail through the relay.");

        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.PasswordResetThrottled);
    }

    [Fact]
    public async Task SendPasswordResetCodeAsync_ThrottlesOnTheNORMALISEDAddress()
    {
        // The key is built from the normalised address, so varying case and padding must NOT buy
        // a fresh allowance. Without normalisation the cache key differs and the second call
        // sails through -- the whole limiter is then bypassable by holding down the shift key.
        var email = NewUnregisteredEmail("reset-normalise");

        await _externalAccountAppService.SendPasswordResetCodeAsync(
            new SendPasswordResetCodeInput { Email = email });

        var ex = await Should.ThrowAsync<BusinessException>(
            async () => await _externalAccountAppService.SendPasswordResetCodeAsync(
                new SendPasswordResetCodeInput { Email = "  " + email.ToUpperInvariant() + "  " }),
            "Upper-casing and padding the same address must not escape the cooldown. The limiter "
            + "keys on the NORMALISED form precisely so it cannot be sidestepped this way.");

        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.PasswordResetThrottled);
    }

    [Fact]
    public async Task SendPasswordResetCodeAsync_WithABlankEmail_IsRejectedBeforeReachingTheLimiter()
    {
        // WRITTEN THE OTHER WAY ROUND FIRST, AND THE RUN CORRECTED ME. The guess was that a blank
        // address falls through the service's own `normalizedEmail.Length == 0` early return and
        // comes back silently. It does not: SendPasswordResetCodeInput.Email carries [Required]
        // and [EmailAddress], and ABP's validation interceptor rejects the call before the method
        // body executes at all.
        //
        // WHICH MEANS THAT EARLY RETURN (ExternalAccountAppService.cs:105-108) IS UNREACHABLE
        // THROUGH THIS INTERFACE. Not dead code -- it is defence in depth, and it would matter to
        // a caller holding the concrete class rather than the validated proxy -- but no test can
        // reach it from here, and a Fact claiming to cover it would be asserting the interceptor's
        // behaviour while appearing to assert the service's.
        //
        // The reachable guarantee is the one worth pinning: a blank address is refused outright
        // and never touches the rate limiter, so it cannot stamp a key.
        await Should.ThrowAsync<AbpValidationException>(
            async () => await _externalAccountAppService.SendPasswordResetCodeAsync(
                new SendPasswordResetCodeInput { Email = "   " }),
            "A blank address must be refused by validation. If this stops throwing, input "
            + "validation has been loosened on an anonymous endpoint and the limiter is now the "
            + "first thing an empty submission reaches.");
    }

    // ------------------------------------------------------------------------
    // ResetPasswordAsync -- the two refusals that run before any Identity work.
    // ------------------------------------------------------------------------

    [Fact]
    public async Task ResetPasswordAsync_WhenTheConfirmationDoesNotMatch_IsRefused()
    {
        // EnsurePasswordsMatch is already unit-tested in isolation. What is NOT covered is that
        // ResetPasswordAsync CALLS it: delete the call and those unit tests stay green, because
        // the helper they exercise is still correct -- merely no longer consulted.
        await Should.ThrowAsync<UserFriendlyException>(
            async () => await _externalAccountAppService.ResetPasswordAsync(
                new ResetPasswordInput
                {
                    UserId = Guid.NewGuid(),
                    ResetToken = "TEST-token",
                    Password = "Test123!",
                    ConfirmPassword = "Test123?",
                }),
            "A mismatched confirmation must be refused. This runs BEFORE the user lookup, so it "
            + "is reachable with an id that resolves to nobody.");
    }

    [Fact]
    public async Task ResetPasswordAsync_ForAUserThatDoesNotExist_ReportsAnInvalidToken()
    {
        // Deliberately NOT a "user not found" error. Reporting the real reason would turn this
        // endpoint into an oracle for which user ids exist, so an unknown id and a bad token are
        // made indistinguishable.
        var ex = await Should.ThrowAsync<BusinessException>(
            async () => await _externalAccountAppService.ResetPasswordAsync(
                new ResetPasswordInput
                {
                    UserId = Guid.NewGuid(),
                    ResetToken = "TEST-token",
                    Password = "Test123!",
                    ConfirmPassword = "Test123!",
                }));

        ex.Code.ShouldBe(
            CaseEvaluationDomainErrorCodes.ResetPasswordTokenInvalid,
            "An unresolvable user must surface as an INVALID TOKEN, never as user-not-found. "
            + "Any distinct error here lets a caller enumerate which user ids are real.");
    }
}
