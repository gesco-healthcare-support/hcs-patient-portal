using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.ExternalAccount;
using HealthcareSupport.CaseEvaluation.Pages.Account;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AuthServer.Tests;

/// <summary>
/// The AuthServer's own account pages: forgot password, reset password, resend verification, the
/// start of email confirmation, logout and the locked-out page. Each is a plain page model over a
/// substituted account service, built without a running host.
/// </summary>
/// <remarks>
/// <para>
/// The anti-enumeration rule these pages exist for is asserted directly: forgot-password and
/// resend-verification return the SAME generic success when the account service throws, so neither
/// the response nor the page reveals whether the address is registered. The one deliberate
/// exception, the reset throttle, is asserted too.
/// </para>
/// <para>
/// NOT COVERED: email confirmation after the user lookup (it needs ABP's concrete
/// <c>IdentityUserManager</c>), and the custom <c>LoginModel</c>, whose overrides wrap the
/// obfuscated ABP Pro base page.
/// </para>
/// </remarks>
public class AccountPageModelTests
{
    private const string Email = "TEST-account.user@test.local";
    private static readonly Guid UserId = Guid.Parse("7e570000-0000-4000-9000-00000000c001");

    /// <summary>Gives a page model an HTTP context, model state and working TempData.</summary>
    private static T WithContext<T>(T model, IServiceProvider? services = null, ClaimsPrincipal? user = null)
        where T : PageModel
    {
        var httpContext = new DefaultHttpContext
        {
            RequestServices = services ?? new ServiceCollection().BuildServiceProvider(),
        };
        if (user != null)
        {
            httpContext.User = user;
        }
        model.PageContext = new PageContext { HttpContext = httpContext };
        model.TempData = new TempDataDictionary(httpContext, Substitute.For<ITempDataProvider>());
        return model;
    }

    // ---------------------------------------------------------------- forgot password

    [Fact]
    public async Task Forgot_password_sends_the_reset_code_and_shows_the_generic_success()
    {
        var account = Substitute.For<IExternalAccountAppService>();
        var model = WithContext(new ForgotPasswordModel(account, NullLogger<ForgotPasswordModel>.Instance)
        {
            Email = Email,
            ReturnUrl = "/TEST-return",
        });

        (await model.OnPostAsync()).ShouldBeOfType<PageResult>();

        await account.Received(1).SendPasswordResetCodeAsync(
            Arg.Is<SendPasswordResetCodeInput>(i => i.Email == Email && i.ReturnUrl == "/TEST-return"));
        model.RequestSubmitted.ShouldBeTrue();
        model.IsThrottled.ShouldBeFalse();
    }

    [Fact]
    public async Task Forgot_password_shows_the_same_generic_success_when_the_service_throws()
    {
        var account = Substitute.For<IExternalAccountAppService>();
        account.SendPasswordResetCodeAsync(Arg.Any<SendPasswordResetCodeInput>())
            .ThrowsAsync(new InvalidOperationException("TEST-dispatch-failure"));
        var model = WithContext(new ForgotPasswordModel(account, NullLogger<ForgotPasswordModel>.Instance) { Email = Email });

        (await model.OnPostAsync()).ShouldBeOfType<PageResult>();

        model.RequestSubmitted.ShouldBeTrue("a failure must look exactly like a success, or the page enumerates accounts");
        model.IsThrottled.ShouldBeFalse();
    }

    /// <summary>A logger with Information enabled that records each entry's level and message.</summary>
    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }

    [Fact]
    public async Task Forgot_password_says_wait_when_the_reset_is_throttled_and_logs_the_throttle()
    {
        var account = Substitute.For<IExternalAccountAppService>();
        account.SendPasswordResetCodeAsync(Arg.Any<SendPasswordResetCodeInput>())
            .ThrowsAsync(new BusinessException(CaseEvaluationDomainErrorCodes.PasswordResetThrottled));
        var logger = new RecordingLogger<ForgotPasswordModel>();
        var model = WithContext(new ForgotPasswordModel(account, logger) { Email = Email });

        (await model.OnPostAsync()).ShouldBeOfType<PageResult>();

        model.IsThrottled.ShouldBeTrue();
        model.RequestSubmitted.ShouldBeFalse();
        logger.Entries.ShouldContain(e => e.Level == LogLevel.Information && e.Message.Contains("reset throttled"));
    }

    [Fact]
    public async Task Forgot_password_sends_nothing_for_an_invalid_form()
    {
        var account = Substitute.For<IExternalAccountAppService>();
        var model = WithContext(new ForgotPasswordModel(account, NullLogger<ForgotPasswordModel>.Instance) { Email = Email });
        model.ModelState.AddModelError(nameof(ForgotPasswordModel.Email), "Enter a valid email address.");

        (await model.OnPostAsync()).ShouldBeOfType<PageResult>();
        model.OnGet().ShouldBeOfType<PageResult>();

        await account.DidNotReceiveWithAnyArgs().SendPasswordResetCodeAsync(default!);
        model.RequestSubmitted.ShouldBeFalse();
    }

    // ---------------------------------------------------------------- reset password

    private static ResetPasswordModel ResetModel(IExternalAccountAppService account, string? returnUrl = null) =>
        WithContext(new ResetPasswordModel(account, NullLogger<ResetPasswordModel>.Instance)
        {
            UserId = UserId,
            ResetToken = "TEST-reset-token",
            Password = "TEST-Passw0rd!",
            ConfirmPassword = "TEST-Passw0rd!",
            ReturnUrl = returnUrl,
        });

    [Fact]
    public void Reset_password_without_a_user_or_token_sends_the_user_back_to_forgot_password()
    {
        var account = Substitute.For<IExternalAccountAppService>();
        var noToken = ResetModel(account);
        noToken.ResetToken = " ";
        var noUser = ResetModel(account);
        noUser.UserId = Guid.Empty;

        foreach (var model in new[] { noToken, noUser })
        {
            model.OnGet().ShouldBeOfType<RedirectToPageResult>().PageName.ShouldBe("./ForgotPassword");
            model.TempData["ErrorMessage"].ShouldBe("That reset link doesn't work anymore. Request a new one below.");
        }

        ResetModel(account).OnGet().ShouldBeOfType<PageResult>();
    }

    [Fact]
    public async Task A_successful_reset_goes_to_login_with_the_password_updated_flash_and_keeps_the_return_url()
    {
        var account = Substitute.For<IExternalAccountAppService>();
        var model = ResetModel(account, returnUrl: "/connect/authorize?client_id=TEST");

        var result = (await model.OnPostAsync()).ShouldBeOfType<LocalRedirectResult>();

        result.Url.ShouldBe("~/Account/Login?flash=password-updated&ReturnUrl=%2Fconnect%2Fauthorize%3Fclient_id%3DTEST");
        await account.Received(1).ResetPasswordAsync(Arg.Is<ResetPasswordInput>(i =>
            i.UserId == UserId && i.ResetToken == "TEST-reset-token" && i.Password == "TEST-Passw0rd!"));
    }

    [Fact]
    public async Task An_expired_reset_token_sends_the_user_back_to_forgot_password()
    {
        var account = Substitute.For<IExternalAccountAppService>();
        account.ResetPasswordAsync(Arg.Any<ResetPasswordInput>())
            .ThrowsAsync(new BusinessException(CaseEvaluationDomainErrorCodes.ResetPasswordTokenInvalid));
        var model = ResetModel(account);

        (await model.OnPostAsync()).ShouldBeOfType<RedirectToPageResult>().PageName.ShouldBe("./ForgotPassword");
        model.TempData["ErrorMessage"].ShouldBe("That reset link doesn't work anymore. Request a new one below.");
    }

    [Fact]
    public async Task A_password_policy_failure_is_shown_on_the_form_and_any_other_failure_gets_a_generic_message()
    {
        var policy = Substitute.For<IExternalAccountAppService>();
        policy.ResetPasswordAsync(Arg.Any<ResetPasswordInput>())
            .ThrowsAsync(new UserFriendlyException("Passwords must have at least one digit."));
        var policyModel = ResetModel(policy);
        (await policyModel.OnPostAsync()).ShouldBeOfType<PageResult>();
        policyModel.ErrorMessage.ShouldBe("Passwords must have at least one digit.");

        var broken = Substitute.For<IExternalAccountAppService>();
        broken.ResetPasswordAsync(Arg.Any<ResetPasswordInput>())
            .ThrowsAsync(new InvalidOperationException("TEST-unexpected"));
        var brokenModel = ResetModel(broken);
        (await brokenModel.OnPostAsync()).ShouldBeOfType<PageResult>();
        brokenModel.ErrorMessage.ShouldBe("We could not reset your password. Please try again or request a new reset link.");
    }

    [Fact]
    public async Task Reset_password_sends_nothing_for_an_invalid_form()
    {
        var account = Substitute.For<IExternalAccountAppService>();
        var model = ResetModel(account);
        model.ModelState.AddModelError(nameof(ResetPasswordModel.ConfirmPassword), "Passwords don't match.");

        (await model.OnPostAsync()).ShouldBeOfType<PageResult>();

        await account.DidNotReceiveWithAnyArgs().ResetPasswordAsync(default!);
    }

    // ---------------------------------------------------------------- resend verification

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Resend_verification_sends_on_post_and_shows_the_generic_success_even_when_it_fails(bool serviceThrows)
    {
        var account = Substitute.For<IExternalAccountAppService>();
        if (serviceThrows)
        {
            account.ResendEmailVerificationAsync(Arg.Any<ResendEmailVerificationInput>())
                .ThrowsAsync(new InvalidOperationException("TEST-dispatch-failure"));
        }
        var model = WithContext(new ResendVerificationModel(account, NullLogger<ResendVerificationModel>.Instance) { Email = Email });

        (await model.OnPostAsync()).ShouldBeOfType<PageResult>();

        await account.Received(1).ResendEmailVerificationAsync(Arg.Is<ResendEmailVerificationInput>(i => i.Email == Email));
        model.RequestSubmitted.ShouldBeTrue();
        model.GetHeading().ShouldBe("Verify your email");
        model.GetIntro().ShouldContain("Resend below");
    }

    [Theory]
    [InlineData("1", true, false)]
    [InlineData("1", true, true)]
    [InlineData("0", false, false)]
    [InlineData(null, false, false)]
    public async Task Resend_verification_sends_on_get_only_when_autosend_is_1(string? autosend, bool expectSend, bool serviceThrows)
    {
        var account = Substitute.For<IExternalAccountAppService>();
        if (serviceThrows)
        {
            account.ResendEmailVerificationAsync(Arg.Any<ResendEmailVerificationInput>())
                .ThrowsAsync(new InvalidOperationException("TEST-dispatch-failure"));
        }
        var model = WithContext(new ResendVerificationModel(account, NullLogger<ResendVerificationModel>.Instance)
        {
            Email = Email,
            Autosend = autosend,
        });

        (await model.OnGetAsync()).ShouldBeOfType<PageResult>();

        await account.Received(expectSend ? 1 : 0).ResendEmailVerificationAsync(Arg.Any<ResendEmailVerificationInput>());
        model.RequestSubmitted.ShouldBe(expectSend);
    }

    [Fact]
    public async Task Resend_verification_sends_nothing_for_an_invalid_form()
    {
        var account = Substitute.For<IExternalAccountAppService>();
        var model = WithContext(new ResendVerificationModel(account, NullLogger<ResendVerificationModel>.Instance) { Email = Email });
        model.ModelState.AddModelError(nameof(ResendVerificationModel.Email), "Enter a valid email address.");

        (await model.OnPostAsync()).ShouldBeOfType<PageResult>();

        await account.DidNotReceiveWithAnyArgs().ResendEmailVerificationAsync(default!);
        model.RequestSubmitted.ShouldBeFalse();
    }

    // ---------------------------------------------------------------- email confirmation (entry guard)

    [Theory]
    [InlineData(false, "TEST-token")]
    [InlineData(true, " ")]
    public async Task Email_confirmation_without_a_user_or_token_goes_to_login_as_invalid_keeping_the_return_urls(
        bool hasUser, string token)
    {
        // The user manager is never reached on this path, so none is supplied.
        var model = WithContext(new EmailConfirmationModel(null!, NullLogger<EmailConfirmationModel>.Instance)
        {
            UserId = hasUser ? UserId : Guid.Empty,
            ConfirmationToken = token,
            ReturnUrl = "/TEST return",
            ReturnUrlHash = "#TEST",
        });

        var result = (await model.OnGetAsync()).ShouldBeOfType<LocalRedirectResult>();

        result.Url.ShouldBe("~/Account/Login?flash=verification-invalid&ReturnUrl=%2FTEST%20return&ReturnUrlHash=%23TEST");
    }

    // ---------------------------------------------------------------- logout + locked out

    private sealed class RecordingAuthentication : IAuthenticationService
    {
        public List<string?> SignedOut { get; } = new();

        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
        {
            SignedOut.Add(scheme);
            return Task.CompletedTask;
        }

        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) =>
            Task.FromResult(AuthenticateResult.NoResult());

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;

        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;

        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties) =>
            Task.CompletedTask;
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Logout_signs_out_of_every_identity_scheme_only_when_signed_in_and_always_clears_the_cookies(bool signedIn)
    {
        var auth = new RecordingAuthentication();
        var services = new ServiceCollection().AddSingleton<IAuthenticationService>(auth).BuildServiceProvider();
        var user = signedIn
            ? new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "TEST-user") }, "Test"))
            : new ClaimsPrincipal(new ClaimsIdentity());
        var model = WithContext(new LogoutModel(NullLogger<LogoutModel>.Instance), services, user);

        (await model.OnGetAsync()).ShouldBeOfType<RedirectToPageResult>().PageName.ShouldBe("./Login");

        if (signedIn)
        {
            auth.SignedOut.ShouldBe(new[]
            {
                IdentityConstants.ApplicationScheme,
                IdentityConstants.ExternalScheme,
                IdentityConstants.TwoFactorUserIdScheme,
                IdentityConstants.TwoFactorRememberMeScheme,
            });
        }
        else
        {
            auth.SignedOut.ShouldBeEmpty();
        }

        var setCookies = model.HttpContext.Response.Headers.SetCookie.ToString();
        setCookies.ShouldContain("__tenant=;");
        setCookies.ShouldContain("XSRF-TOKEN=;");
    }

    [Fact]
    public void The_locked_out_page_shows_the_remaining_time_handed_over_by_login_or_the_generic_wording()
    {
        var handedOver = WithContext(new LockedOutModel());
        handedOver.TempData[LoginModel.LockoutRemainingTempDataKey] = "TEST 4 minutes";
        handedOver.OnGet().ShouldBeOfType<PageResult>();
        handedOver.RemainingText.ShouldBe("TEST 4 minutes");

        var nothing = WithContext(new LockedOutModel());
        nothing.OnGet().ShouldBeOfType<PageResult>();
        nothing.RemainingText.ShouldBe(LockoutRemainingText.Unknown);

        var blank = WithContext(new LockedOutModel());
        blank.TempData[LoginModel.LockoutRemainingTempDataKey] = "  ";
        blank.OnGet();
        blank.RemainingText.ShouldBe(LockoutRemainingText.Unknown);
    }
}
