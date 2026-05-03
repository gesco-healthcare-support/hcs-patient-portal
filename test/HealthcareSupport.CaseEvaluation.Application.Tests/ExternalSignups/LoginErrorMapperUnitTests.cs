using Microsoft.AspNetCore.Identity;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.ExternalSignups;

/// <summary>
/// Phase 9 (2026-05-03) -- pure unit tests for
/// <see cref="LoginErrorMapper"/>. Bypasses the ABP integration test
/// harness (currently blocked by the ABP Pro license gate per
/// <c>docs/handoffs/2026-05-03-test-host-license-blocker.md</c>) by
/// invoking the static helper via the <c>InternalsVisibleTo</c> hook in
/// the Application assembly's <c>AssemblyInfo.cs</c>.
///
/// Coverage:
/// <list type="bullet">
///   <item>Each of the four OLD-verbatim error keys is reachable (audit L1).</item>
///   <item>EmailNotConfirmed and EmailNotConfirmedAndNotAllowed both surface
///         the resend link (audit Q2 resolution).</item>
///   <item>Unknown / null / empty codes default to InvalidUsernameOrPassword
///         (safest fallthrough; avoids info leak about which codes match).</item>
///   <item>Comparison is case-insensitive ordinal.</item>
///   <item><see cref="LoginErrorMapper.MapIdentityResult"/> walks
///         IdentityResult.Errors and surfaces the first matching key.</item>
///   <item>Localization-key constants match the expected `Login:*`
///         identifiers documented in the audit doc.</item>
/// </list>
/// </summary>
public class LoginErrorMapperUnitTests
{
    // ------------------------------------------------------------------
    // EmailNotConfirmed -> VerificationLinkSent
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("EmailNotConfirmed")]
    [InlineData("EmailNotConfirmedAndNotAllowed")]
    [InlineData("emailnotconfirmed")] // case-insensitive ordinal
    [InlineData("EMAILNOTCONFIRMED")]
    public void MapAbpErrorToLocalizationKey_EmailNotConfirmedFamily_ReturnsVerificationLinkSent(string code)
    {
        LoginErrorMapper.MapAbpErrorToLocalizationKey(code)
            .ShouldBe(LoginErrorMapper.VerificationLinkSent);
    }

    [Theory]
    [InlineData("EmailNotConfirmed")]
    [InlineData("EmailNotConfirmedAndNotAllowed")]
    public void ShouldShowResendLink_EmailNotConfirmedFamily_ReturnsTrue(string code)
    {
        LoginErrorMapper.ShouldShowResendLink(code).ShouldBeTrue();
    }

    [Theory]
    [InlineData("LockedOut")]
    [InlineData("InvalidLogin")]
    [InlineData("UserNotFound")]
    [InlineData("PasswordMismatch")]
    [InlineData("")]
    [InlineData(null)]
    public void ShouldShowResendLink_NotEmailNotConfirmed_ReturnsFalse(string? code)
    {
        LoginErrorMapper.ShouldShowResendLink(code).ShouldBeFalse();
    }

    // ------------------------------------------------------------------
    // Account inactive / locked out -> AccountNotActivated
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("LockedOut")]
    [InlineData("NotAllowed")]
    [InlineData("IsNotActive")]
    [InlineData("UserLockedOut")]
    [InlineData("lockedout")]
    public void MapAbpErrorToLocalizationKey_InactiveFamily_ReturnsAccountNotActivated(string code)
    {
        LoginErrorMapper.MapAbpErrorToLocalizationKey(code)
            .ShouldBe(LoginErrorMapper.AccountNotActivated);
    }

    // ------------------------------------------------------------------
    // User not found / soft deleted -> UserNotExist
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("UserNotFound")]
    [InlineData("InvalidUserName")]
    [InlineData("UserDoesNotExist")]
    [InlineData("usernotfound")]
    public void MapAbpErrorToLocalizationKey_UserNotFoundFamily_ReturnsUserNotExist(string code)
    {
        LoginErrorMapper.MapAbpErrorToLocalizationKey(code)
            .ShouldBe(LoginErrorMapper.UserNotExist);
    }

    // ------------------------------------------------------------------
    // Credential mismatch -> InvalidUsernameOrPassword
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("InvalidLogin")]
    [InlineData("PasswordMismatch")]
    [InlineData("InvalidPassword")]
    [InlineData("invalidlogin")]
    public void MapAbpErrorToLocalizationKey_CredentialMismatchFamily_ReturnsInvalidUsernameOrPassword(string code)
    {
        LoginErrorMapper.MapAbpErrorToLocalizationKey(code)
            .ShouldBe(LoginErrorMapper.InvalidUsernameOrPassword);
    }

    // ------------------------------------------------------------------
    // Unknown / null / empty -> InvalidUsernameOrPassword (safest default)
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("SomeUnknownCode")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void MapAbpErrorToLocalizationKey_Unknown_ReturnsInvalidUsernameOrPassword(string? code)
    {
        LoginErrorMapper.MapAbpErrorToLocalizationKey(code)
            .ShouldBe(LoginErrorMapper.InvalidUsernameOrPassword);
    }

    // ------------------------------------------------------------------
    // MapIdentityResult walks Errors collection
    // ------------------------------------------------------------------

    [Fact]
    public void MapIdentityResult_NullResult_ReturnsInvalidUsernameOrPassword()
    {
        LoginErrorMapper.MapIdentityResult(null)
            .ShouldBe(LoginErrorMapper.InvalidUsernameOrPassword);
    }

    [Fact]
    public void MapIdentityResult_SuccessResult_ReturnsInvalidUsernameOrPasswordFallback()
    {
        // Success has no Errors; mapper falls through to default.
        LoginErrorMapper.MapIdentityResult(IdentityResult.Success)
            .ShouldBe(LoginErrorMapper.InvalidUsernameOrPassword);
    }

    [Fact]
    public void MapIdentityResult_FailedWithEmailNotConfirmed_ReturnsVerificationLinkSent()
    {
        var result = IdentityResult.Failed(new IdentityError
        {
            Code = "EmailNotConfirmed",
            Description = "Email not confirmed",
        });

        LoginErrorMapper.MapIdentityResult(result)
            .ShouldBe(LoginErrorMapper.VerificationLinkSent);
    }

    [Fact]
    public void MapIdentityResult_FailedWithLockedOut_ReturnsAccountNotActivated()
    {
        var result = IdentityResult.Failed(new IdentityError
        {
            Code = "LockedOut",
            Description = "Account locked",
        });

        LoginErrorMapper.MapIdentityResult(result)
            .ShouldBe(LoginErrorMapper.AccountNotActivated);
    }

    [Fact]
    public void MapIdentityResult_MultipleErrors_PicksFirst()
    {
        // First mapped error wins; ensures deterministic UX surface.
        var result = IdentityResult.Failed(
            new IdentityError { Code = "EmailNotConfirmed", Description = "" },
            new IdentityError { Code = "PasswordMismatch", Description = "" });

        LoginErrorMapper.MapIdentityResult(result)
            .ShouldBe(LoginErrorMapper.VerificationLinkSent);
    }

    [Fact]
    public void MapIdentityResult_ErrorWithNullCode_SkipsAndReturnsDefault()
    {
        var result = IdentityResult.Failed(new IdentityError
        {
            Code = null!,
            Description = "Something",
        });

        LoginErrorMapper.MapIdentityResult(result)
            .ShouldBe(LoginErrorMapper.InvalidUsernameOrPassword);
    }

    // ------------------------------------------------------------------
    // Constants -- match en.json
    // ------------------------------------------------------------------

    [Fact]
    public void Constants_AreOldVerbatimLocalizationKeys()
    {
        LoginErrorMapper.UserNotExist.ShouldBe("Login:UserNotExist");
        LoginErrorMapper.InvalidUsernameOrPassword.ShouldBe("Login:InvalidUsernameOrPassword");
        LoginErrorMapper.AccountNotActivated.ShouldBe("Login:AccountNotActivated");
        LoginErrorMapper.VerificationLinkSent.ShouldBe("Login:VerificationLinkSent");
    }

    // ------------------------------------------------------------------
    // CoerceBool -- ABP extra-property normalization
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void CoerceBool_BoolPassesThrough(bool input, bool expected)
    {
        ExternalSignupAppService.CoerceBool(input).ShouldBe(expected);
    }

    [Theory]
    [InlineData("True", true)]
    [InlineData("true", true)]
    [InlineData("FALSE", false)]
    [InlineData("False", false)]
    [InlineData("", false)]
    [InlineData("not-a-bool", false)]
    public void CoerceBool_StringTryParse(string? input, bool expected)
    {
        ExternalSignupAppService.CoerceBool(input).ShouldBe(expected);
    }

    [Fact]
    public void CoerceBool_Null_ReturnsFalse()
    {
        ExternalSignupAppService.CoerceBool(null).ShouldBeFalse();
    }

    [Fact]
    public void CoerceBool_UnrecognizedType_ReturnsFalse()
    {
        ExternalSignupAppService.CoerceBool(42).ShouldBeFalse();
        ExternalSignupAppService.CoerceBool(new object()).ShouldBeFalse();
    }
}
