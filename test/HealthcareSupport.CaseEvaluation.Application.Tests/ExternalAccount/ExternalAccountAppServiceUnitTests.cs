using System;
using System.Reflection;
using HealthcareSupport.CaseEvaluation.ExternalAccount;
using Microsoft.AspNetCore.Identity;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Identity;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.ExternalAccount;

/// <summary>
/// Phase 10 (2026-05-03) -- pure unit tests for the
/// <see cref="PasswordResetGate"/> + <see cref="ExternalAccountAppService"/>
/// pure helpers.
///
/// <para>These intentionally bypass ABP's integration test harness (which
/// exhibits a pre-existing Phase 4 license-checker test-host crash unrelated
/// to this work). They exercise the gate, normalization, password-match,
/// URL builder, and IdentityResult classifier directly via the
/// <c>internal</c> accessor exposed to this assembly through
/// <c>InternalsVisibleTo</c>.</para>
///
/// <para>End-to-end DB / SMTP / multi-tenant assertions live in a follow-up
/// integration suite gated on the test-host crash being resolved.</para>
/// </summary>
public class ExternalAccountAppServiceUnitTests
{
    // ------------------------------------------------------------------
    // PasswordResetGate.EnsureUserCanRequestReset
    // OLD parity: UserAuthenticationDomain.cs:166-173
    // ------------------------------------------------------------------

    [Fact]
    public void EnsureUserCanRequestReset_NullUser_ReturnsSilently()
    {
        // Account-enumeration mitigation: caller treats a missing user
        // as "if registered, check your email". OLD bug-fix exception:
        // OLD line 177 returned UserNotExist here which leaked.
        Should.NotThrow(() => PasswordResetGate.EnsureUserCanRequestReset(null));
    }

    [Fact]
    public void EnsureUserCanRequestReset_UnconfirmedEmail_Throws()
    {
        var user = NewUser(emailConfirmed: false, isActive: true);

        var ex = Should.Throw<BusinessException>(
            () => PasswordResetGate.EnsureUserCanRequestReset(user));

        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.EmailNotConfirmedForPasswordReset);
    }

    [Fact]
    public void EnsureUserCanRequestReset_InactiveUser_Throws()
    {
        var user = NewUser(emailConfirmed: true, isActive: false);

        var ex = Should.Throw<BusinessException>(
            () => PasswordResetGate.EnsureUserCanRequestReset(user));

        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.UserInactiveForPasswordReset);
    }

    [Fact]
    public void EnsureUserCanRequestReset_VerifiedActiveUser_DoesNotThrow()
    {
        var user = NewUser(emailConfirmed: true, isActive: true);

        Should.NotThrow(() => PasswordResetGate.EnsureUserCanRequestReset(user));
    }

    [Fact]
    public void EnsureUserCanRequestReset_PrecedenceUnconfirmedBeforeInactive_ReportsUnconfirmedFirst()
    {
        // Both gates fail on the same user. OLD evaluated EmailConfirmed
        // FIRST (line 167), so we must report EmailNotConfirmed -- not
        // UserInactive -- to keep the visible error verbatim.
        var user = NewUser(emailConfirmed: false, isActive: false);

        var ex = Should.Throw<BusinessException>(
            () => PasswordResetGate.EnsureUserCanRequestReset(user));

        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.EmailNotConfirmedForPasswordReset);
    }

    // ------------------------------------------------------------------
    // ExternalAccountAppService.NormalizeEmail
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("USER@EXAMPLE.COM", "user@example.com")]
    [InlineData("  user@Example.com  ", "user@example.com")]
    [InlineData("user@example.com", "user@example.com")]
    public void NormalizeEmail_TrimsAndLowercases(string input, string expected)
    {
        ExternalAccountAppService.NormalizeEmail(input).ShouldBe(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NormalizeEmail_NullOrWhitespace_ReturnsEmpty(string? input)
    {
        ExternalAccountAppService.NormalizeEmail(input).ShouldBe(string.Empty);
    }

    // ------------------------------------------------------------------
    // ExternalAccountAppService.EnsurePasswordsMatch
    // OLD parity: UserAuthenticationDomain.cs:222
    // ------------------------------------------------------------------

    [Fact]
    public void EnsurePasswordsMatch_Equal_DoesNotThrow()
    {
        Should.NotThrow(() =>
            ExternalAccountAppService.EnsurePasswordsMatch("Test123!", "Test123!"));
    }

    [Fact]
    public void EnsurePasswordsMatch_Mismatch_Throws()
    {
        Should.Throw<UserFriendlyException>(() =>
            ExternalAccountAppService.EnsurePasswordsMatch("Test123!", "Test123?"));
    }

    [Fact]
    public void EnsurePasswordsMatch_DiffersInCase_Throws()
    {
        // Ordinal compare: case-sensitive.
        Should.Throw<UserFriendlyException>(() =>
            ExternalAccountAppService.EnsurePasswordsMatch("Test123!", "TEST123!"));
    }

    [Fact]
    public void EnsurePasswordsMatch_TrailingWhitespaceMismatch_Throws()
    {
        Should.Throw<UserFriendlyException>(() =>
            ExternalAccountAppService.EnsurePasswordsMatch("Test123!", "Test123! "));
    }

    // ------------------------------------------------------------------
    // ExternalAccountAppService.BuildResetUrl
    // ------------------------------------------------------------------

    [Fact]
    public void BuildResetUrl_NoReturnUrl_BuildsBaseQuery()
    {
        var userId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var token = "raw-token-with-special-chars+/=";

        var url = ExternalAccountAppService.BuildResetUrl(
            authServerBaseUrl: "https://localhost:44368",
            userId: userId,
            resetToken: token,
            returnUrl: null);

        url.ShouldStartWith("https://localhost:44368/Account/ResetPassword?userId=11111111-2222-3333-4444-555555555555");
        url.ShouldContain("&resetToken=");
        // Token is URL-encoded so '+' / '/' / '=' do not corrupt the query.
        url.ShouldNotContain("token-with-special-chars+/=");
    }

    [Fact]
    public void BuildResetUrl_WithReturnUrl_AppendsEncodedReturnUrl()
    {
        var url = ExternalAccountAppService.BuildResetUrl(
            authServerBaseUrl: "https://localhost:44368",
            userId: Guid.Empty,
            resetToken: "abc",
            returnUrl: "https://app/page?x=1&y=2");

        url.ShouldContain("&returnUrl=");
        url.ShouldNotContain("https://app/page?x=1&y=2");
    }

    [Fact]
    public void BuildResetUrl_EmptyReturnUrl_DoesNotAppendReturnUrlSegment()
    {
        var url = ExternalAccountAppService.BuildResetUrl(
            authServerBaseUrl: "https://localhost:44368",
            userId: Guid.Empty,
            resetToken: "abc",
            returnUrl: "");

        url.ShouldNotContain("returnUrl");
    }

    // ------------------------------------------------------------------
    // ExternalAccountAppService.IsTokenFailure
    // ------------------------------------------------------------------

    [Fact]
    public void IsTokenFailure_InvalidTokenError_ReturnsTrue()
    {
        var result = IdentityResult.Failed(new IdentityError
        {
            Code = "InvalidToken",
            Description = "Invalid token.",
        });

        ExternalAccountAppService.IsTokenFailure(result).ShouldBeTrue();
    }

    [Fact]
    public void IsTokenFailure_PasswordPolicyError_ReturnsFalse()
    {
        var result = IdentityResult.Failed(new IdentityError
        {
            Code = "PasswordRequiresDigit",
            Description = "Passwords must have at least one digit ('0'-'9').",
        });

        ExternalAccountAppService.IsTokenFailure(result).ShouldBeFalse();
    }

    [Fact]
    public void IsTokenFailure_MultipleErrorsIncludingInvalidToken_ReturnsTrue()
    {
        var result = IdentityResult.Failed(
            new IdentityError { Code = "PasswordRequiresDigit", Description = "x" },
            new IdentityError { Code = "InvalidToken", Description = "x" });

        ExternalAccountAppService.IsTokenFailure(result).ShouldBeTrue();
    }

    [Fact]
    public void IsTokenFailure_NullResult_ReturnsFalse()
    {
        ExternalAccountAppService.IsTokenFailure(null!).ShouldBeFalse();
    }

    [Fact]
    public void IsTokenFailure_SuccessResult_ReturnsFalse()
    {
        ExternalAccountAppService.IsTokenFailure(IdentityResult.Success).ShouldBeFalse();
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static IdentityUser NewUser(bool emailConfirmed, bool isActive)
    {
        var user = new IdentityUser(
            id: Guid.NewGuid(),
            userName: "test@example.com",
            email: "test@example.com",
            tenantId: null);
        // EmailConfirmed (inherited from Microsoft.AspNetCore.Identity
        // IdentityUser<TKey>) and IsActive (declared on Volo.Abp.Identity
        // .IdentityUser) both have <c>protected internal set</c>
        // accessors -- not reachable from this test assembly. Reflection
        // is the cheapest way to populate them for pure unit tests; the
        // alternative (full UserManager.CreateAsync round-trip) needs the
        // ABP integration harness which is gated on the Phase 4
        // license-checker test-host crash.
        SetProperty(user, "EmailConfirmed", emailConfirmed);
        SetProperty(user, "IsActive", isActive);
        return user;
    }

    private static void SetProperty(object instance, string name, object value)
    {
        var prop = instance.GetType().GetProperty(
            name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (prop == null || prop.GetSetMethod(nonPublic: true) == null)
        {
            throw new InvalidOperationException(
                $"Property '{name}' not found or has no setter on {instance.GetType().FullName}.");
        }
        prop.GetSetMethod(nonPublic: true)!.Invoke(instance, new[] { value });
    }
}
