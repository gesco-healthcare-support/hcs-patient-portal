using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using HealthcareSupport.CaseEvaluation.TestData;
using Microsoft.Extensions.Caching.Distributed;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
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
    private readonly IdentityUserManager _userManager;
    private readonly ICurrentTenant _currentTenant;
    private readonly IDistributedCache _cache;

    public EfCoreExternalAccountAppServiceTests()
    {
        _externalAccountAppService = GetRequiredService<IExternalAccountAppService>();
        _userManager = GetRequiredService<IdentityUserManager>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _cache = GetRequiredService<IDistributedCache>();
    }

    private static string EmailFor(string token) => $"TEST-acct-{token}@test.local";

    /// <summary>
    /// The cooldown key the limiter writes, mirroring StampRateLimitAsync's format.
    ///
    /// <para>NOTE THE ToLowerInvariant, which is load-bearing and cost a run to find. The service
    /// keys on the NORMALISED address, so a lookup using the original casing misses every time --
    /// and it misses by returning null, which is indistinguishable from "nothing was stamped".
    /// The first version of this helper had that bug, and the half of the Fact asserting a stamp
    /// IS present is what caught it; the half asserting a stamp is absent passed happily, for
    /// entirely the wrong reason.</para>
    /// </summary>
    private static string CooldownKey(string keyPrefix, string token) =>
        $"{keyPrefix}:cooldown:{EmailFor(token).ToLowerInvariant()}";

    /// <summary>Confirms an already-created user's email through the real token round trip.</summary>
    private async Task ConfirmEmailAsync(Guid userId)
    {
        var user = await _userManager.GetByIdAsync(userId);
        var confirmToken = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        (await _userManager.ConfirmEmailAsync(user, confirmToken)).Succeeded.ShouldBeTrue(
            "Fixture failed to confirm the email mid-test.");
    }

    /// <summary>
    /// Creates a REGISTERED user in TenantA with a controllable confirmation state.
    ///
    /// <para>This is the fixture the whole of item 6's remainder sits behind. Without a real
    /// registered user every flow here returns at its unregistered short-circuit, which is why the
    /// first pass could only reach the refusal paths.</para>
    ///
    /// <para>Email confirmation goes through the REAL token round trip
    /// (GenerateEmailConfirmationTokenAsync then ConfirmEmailAsync) rather than reflecting onto the
    /// property. The existing unit-test file reflects onto it because it has no DI; here the
    /// UserManager is available, and using it means the fixture exercises the same path production
    /// does instead of manufacturing a state production cannot produce.</para>
    /// </summary>
    private async Task<Guid> SeedRegisteredUserAsync(string token, bool confirmEmail)
    {
        var userId = Guid.NewGuid();
        var user = new Volo.Abp.Identity.IdentityUser(
            userId,
            $"TEST-acct-{token}",
            $"TEST-acct-{token}@test.local",
            _currentTenant.Id);

        var created = await _userManager.CreateAsync(user, IdentityUsersTestData.SeedPassword);
        created.Succeeded.ShouldBeTrue(
            "Fixture failed to create the user, so anything asserted below would be asserting "
            + "against a user that does not exist: "
            + string.Join("; ", created.Errors.Select(e => e.Description)));

        if (confirmEmail)
        {
            var confirmToken = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var confirmed = await _userManager.ConfirmEmailAsync(user, confirmToken);
            confirmed.Succeeded.ShouldBeTrue(
                "Fixture failed to confirm the email: "
                + string.Join("; ", confirmed.Errors.Select(e => e.Description)));
        }

        return userId;
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

    // ------------------------------------------------------------------------
    // The registered-user paths. NotificationTemplates are NOT seeded in this rig, so every
    // dispatch below genuinely raises NotificationTemplateNotFound and genuinely hits the catch.
    // That is not a limitation to work around -- it makes the caller-still-sees-success guarantee
    // the live path rather than a branch nothing takes.
    // ------------------------------------------------------------------------

    // ------------------------------------------------------------------------
    // WHAT THESE TWO FACTS DO AND DO NOT PIN -- established by measurement, after I got it wrong
    // in both directions.
    //
    // The send paths CANNOT be observed end-to-end in this rig. NotificationTemplateDataSeedContributor
    // is TENANT-SCOPED: with no tenant in the seed context it seeds only the host-scoped codes, and
    // these flows run inside a tenant. So the render raises NotificationTemplateNotFound, the
    // SPECIFIC catch swallows it, and no outbox row is ever written for a tenant user -- measured
    // directly by querying the outbox and finding nothing.
    //
    // Two consequences worth stating rather than hiding:
    //   1. An outbox assertion here would fail for a rig reason, not a product reason. I wrote one,
    //      it failed, and removing it is the honest response rather than seeding a template myself
    //      and asserting against my own scaffolding.
    //   2. A "confirmed user queues nothing" Fact would pass VACUOUSLY, because nothing is ever
    //      queued for anybody here. I wrote that one too and deleted it. It is the exact shape of
    //      test this epic keeps refusing to ship.
    //
    // What remains is real but narrow: a template fault must stay INSIDE the service. These flows
    // are anonymous and deliberately silent, so a thrown exception would both surface a deployment
    // fault to the caller and confirm that the address is registered. The probe that breaks these
    // is the SPECIFIC NotificationTemplateNotFound catch -- NOT the generic one behind it, which is
    // never reached and whose mutation left both Facts passing.
    //
    // End-to-end send coverage needs per-tenant template seeding in the rig. That is tranche-2
    // fixture work, deliberately not invented here under a deadline.
    // ------------------------------------------------------------------------

    [Fact]
    public async Task SendPasswordResetCodeAsync_ForARegisteredUser_KeepsATemplateFaultInternal()
    {
        var token = Guid.NewGuid().ToString("N")[..8];

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                await SeedRegisteredUserAsync(token, confirmEmail: true);

                await Should.NotThrowAsync(
                    async () => await _externalAccountAppService.SendPasswordResetCodeAsync(
                        new SendPasswordResetCodeInput { Email = EmailFor(token) }),
                    "A missing ResetPassword template must be logged and swallowed, not thrown. If "
                    + "this throws, a seeding fault becomes a 500 on an anonymous endpoint AND an "
                    + "oracle telling an attacker the address IS registered -- the unregistered "
                    + "path returns silently.");
            }
        });
    }

    [Fact]
    public async Task ResendEmailVerificationAsync_ForAnUnconfirmedUser_KeepsATemplateFaultInternal()
    {
        // The first Fact to reach the resend flow's registered-user path at all: the earlier pass
        // could not, because an unregistered address returns before the send.
        var token = Guid.NewGuid().ToString("N")[..8];

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                await SeedRegisteredUserAsync(token, confirmEmail: false);

                await Should.NotThrowAsync(
                    async () => await _externalAccountAppService.ResendEmailVerificationAsync(
                        new ResendEmailVerificationInput { Email = EmailFor(token) }),
                    "A missing UserRegistered template must be logged and swallowed, not thrown.");
            }
        });
    }

    [Fact]
    public async Task ResendEmailVerification_AndPasswordReset_DoNotShareARateLimiter()
    {
        // BEHAVIOURAL, and the strongest Fact available on the resend flow. The two limiters are
        // separate partitions keyed "resend-verify" and "password-reset" with different caps (3/hr
        // against 10/hr), deliberately, because resend is the higher SMTP-flood risk. If the
        // prefixes ever collide, one flow silently starts consuming the other's budget -- and
        // because resend refuses SILENTLY, the only visible symptom would be password reset
        // refusing users who never asked for a resend.
        var token = Guid.NewGuid().ToString("N")[..8];

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                var userId = await SeedRegisteredUserAsync(token, confirmEmail: false);

                // Consume the resend allowance for this address. Resend requires an UNCONFIRMED
                // user, so the account starts that way.
                await _externalAccountAppService.ResendEmailVerificationAsync(
                    new ResendEmailVerificationInput { Email = EmailFor(token) });

                // Then confirm it, because password reset legitimately REFUSES an unconfirmed
                // address (PasswordResetGate.EnsureUserCanRequestReset throws
                // EmailNotConfirmedForPasswordReset). The first version of this Fact skipped this
                // step and read that refusal as the limiters colliding -- the product was right and
                // the test was wrong. Confirming here keeps the address identical across both
                // calls, which is the whole point: same key material, different partitions.
                await ConfirmEmailAsync(userId);

                // The reset partition must be untouched by the resend.
                await Should.NotThrowAsync(
                    async () => await _externalAccountAppService.SendPasswordResetCodeAsync(
                        new SendPasswordResetCodeInput { Email = EmailFor(token) }),
                    "A resend must not consume the PASSWORD RESET allowance for the same address. "
                    + "If this throws PasswordResetThrottled, the two limiters have been collapsed "
                    + "onto one cache key and a resend now locks a user out of resetting.");
            }
        });
    }

    [Fact]
    public async Task ResendEmailVerificationAsync_ForAConfirmedUser_DoesNotConsumeTheAllowance()
    {
        // OBSERVES THE LIMITER'S STORAGE RATHER THAN A RESPONSE, and that is a deliberate choice
        // worth stating: the resend endpoint is SILENT on every branch by design, so there is no
        // response difference between "sent", "already confirmed" and "throttled". Asserting on
        // the cooldown key is the only way to tell them apart from outside.
        //
        // The property is real: the stamp happens AFTER dispatch, so a user who is already
        // confirmed returns before it and must not spend an allowance they never used.
        var confirmedToken = Guid.NewGuid().ToString("N")[..8];
        var unconfirmedToken = Guid.NewGuid().ToString("N")[..8];

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                await SeedRegisteredUserAsync(confirmedToken, confirmEmail: true);
                await SeedRegisteredUserAsync(unconfirmedToken, confirmEmail: false);

                await _externalAccountAppService.ResendEmailVerificationAsync(
                    new ResendEmailVerificationInput { Email = EmailFor(confirmedToken) });
                await _externalAccountAppService.ResendEmailVerificationAsync(
                    new ResendEmailVerificationInput { Email = EmailFor(unconfirmedToken) });

                (await _cache.GetStringAsync(CooldownKey("resend-verify", confirmedToken)))
                    .ShouldBeNull(
                        "An already-confirmed address must not stamp the resend limiter: no mail "
                        + "was sent, so no allowance was spent.");

                (await _cache.GetStringAsync(CooldownKey("resend-verify", unconfirmedToken)))
                    .ShouldNotBeNull(
                        "An unconfirmed address DID reach the send, so it must stamp. Without this "
                        + "half the Fact above is vacuous -- a limiter that never stamps anything "
                        + "would satisfy it too.");
            }
        });
    }

    /// <summary>
    /// The first Fact to use the registered-user fixture, and the one that proves the fixture
    /// DISCRIMINATES rather than merely reaching further into the method.
    ///
    /// <para>Item D (2026-08-22) made a completed reset RESTORE ACCESS. Nothing used to clear the
    /// lockout: Identity's ResetPasswordAsync is password-only, and only a successful sign-in
    /// resets the failure count -- which cannot happen while PreSignInCheck short-circuits on the
    /// lockout. So a locked-out user who did exactly what the system told them to do stayed locked
    /// out for the rest of the window. This is OWASP's named mitigation for lockout-as-denial-of-
    /// service.</para>
    ///
    /// <para>Both assertions fail independently under mutation: deleting the reset breaks the
    /// password one, deleting SetLockoutEndDateAsync breaks the lockout one. Neither would be
    /// reachable at all without a registered user.</para>
    /// </summary>
    [Fact]
    public async Task ResetPasswordAsync_WithAValidToken_ChangesThePasswordAndRestoresAccess()
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        const string newPassword = "Test-User2!";

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                var userId = await SeedRegisteredUserAsync(token, confirmEmail: true);
                var user = await _userManager.GetByIdAsync(userId);

                // Put the account in the exact state item D exists to rescue.
                await _userManager.SetLockoutEnabledAsync(user, true);
                await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddMinutes(30));
                (await _userManager.IsLockedOutAsync(user)).ShouldBeTrue(
                    "FIXTURE PRECONDITION FAILED: the user is not actually locked out, so the "
                    + "restore-access assertion below would pass without the product doing "
                    + "anything. This is the empty-fixture trap and it must fail loudly here.");

                var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);

                await _externalAccountAppService.ResetPasswordAsync(new ResetPasswordInput
                {
                    UserId = userId,
                    ResetToken = resetToken,
                    Password = newPassword,
                    ConfirmPassword = newPassword,
                });

                var after = await _userManager.GetByIdAsync(userId);

                (await _userManager.CheckPasswordAsync(after, newPassword)).ShouldBeTrue(
                    "A reset carrying a valid token must actually change the password.");

                (await _userManager.IsLockedOutAsync(after)).ShouldBeFalse(
                    "A completed reset must RESTORE ACCESS. If this fails, a locked-out user who "
                    + "followed the reset link stays locked out for the rest of the window, which "
                    + "is the lockout-as-denial-of-service that item D closed.");
            }
        });
    }

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
