using System;
using System.Globalization;
using System.Threading;
using Microsoft.AspNetCore.Identity;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.ExternalAccount;

/// <summary>
/// #607 -- the first tests anywhere to exercise <c>ExternalAccountAppService</c>.
///
/// <para>Three of its helpers each carry the comment "Internal for unit-test
/// coverage" and none had a test. That is worth stating plainly: the
/// <c>internal</c> accessibility was widened FOR tests that were never written,
/// so the comment described an intention rather than a fact.</para>
///
/// <para>This surface is anonymous and rate-limited -- password reset and email
/// verification -- and three defects have already survived here. These are the
/// pure branches, which are the ones a unit test can own outright; the
/// tenant-resolution paths need the ABP integration harness and are not
/// attempted here.</para>
/// </summary>
public class ExternalAccountHelpersUnitTests
{
    // ---- NormalizeEmail ------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void NormalizeEmail_BlankInput_ReturnsEmptyString(string? raw)
    {
        // Callers treat empty as the silent-success path: an absent email must
        // not become a lookup key, or every blank request would reverse-match
        // whichever user happens to have an empty NormalizedEmail.
        ExternalAccountAppService.NormalizeEmail(raw).ShouldBe(string.Empty);
    }

    [Theory]
    [InlineData("  Foo@Bar.COM  ", "foo@bar.com")]
    [InlineData("USER@EXAMPLE.ORG", "user@example.org")]
    [InlineData("already@normal.com", "already@normal.com")]
    [InlineData("\tMixed@Case.Net\r\n", "mixed@case.net")]
    public void NormalizeEmail_TrimsAndLowercases(string raw, string expected)
    {
        // Must match the form ABP Identity stores in NormalizedEmail, or the
        // reverse lookup misses and the caller silently takes the "no such
        // user" branch -- which returns success to avoid enumeration, so a
        // failed match is indistinguishable from a delivered email.
        ExternalAccountAppService.NormalizeEmail(raw).ShouldBe(expected);
    }

    [Fact]
    public void NormalizeEmail_IsCultureInvariant()
    {
        // THE TURKISH-I HAZARD, and the reason this test exists rather than
        // just asserting lowercase. Under tr-TR, ToLower("I") is 'ı' (U+0131,
        // dotless) -- so a mutation from ToLowerInvariant to ToLower turns
        // "IT@X.COM" into "ıt@x.com", which matches no stored NormalizedEmail.
        // The user would request a reset, get a success response, and never
        // receive an email. Only a caller in a Turkish locale would be
        // affected, which is exactly the kind of defect that survives.
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
            ExternalAccountAppService.NormalizeEmail("IT@EXAMPLE.COM")
                .ShouldBe("it@example.com");
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    // ---- EnsurePasswordsMatch -------------------------------------------

    [Fact]
    public void EnsurePasswordsMatch_IdenticalPasswords_DoesNotThrow()
    {
        Should.NotThrow(() =>
            ExternalAccountAppService.EnsurePasswordsMatch("Str0ng!Pass", "Str0ng!Pass"));
    }

    [Theory]
    [InlineData("Str0ng!Pass", "Different1!")]
    [InlineData("Str0ng!Pass", "")]
    [InlineData("", "Str0ng!Pass")]
    public void EnsurePasswordsMatch_Mismatch_Throws(string password, string confirm)
    {
        Should.Throw<UserFriendlyException>(() =>
            ExternalAccountAppService.EnsurePasswordsMatch(password, confirm));
    }

    [Fact]
    public void EnsurePasswordsMatch_CaseDifference_Throws()
    {
        // Ordinal, not OrdinalIgnoreCase. A case-insensitive compare would
        // accept a confirmation the user did not type and set a password
        // differing from what they believe they chose.
        Should.Throw<UserFriendlyException>(() =>
            ExternalAccountAppService.EnsurePasswordsMatch("Str0ng!Pass", "str0ng!pass"));
    }

    [Fact]
    public void EnsurePasswordsMatch_TrailingWhitespaceDifference_Throws()
    {
        // Deliberately NOT trimmed: a trailing space is part of the password,
        // and silently accepting it here would store one value while the user
        // later types another.
        Should.Throw<UserFriendlyException>(() =>
            ExternalAccountAppService.EnsurePasswordsMatch("Str0ng!Pass", "Str0ng!Pass "));
    }

    // ---- IsTokenFailure --------------------------------------------------

    [Fact]
    public void IsTokenFailure_NullResult_IsFalse()
    {
        ExternalAccountAppService.IsTokenFailure(null!).ShouldBeFalse();
    }

    [Fact]
    public void IsTokenFailure_Success_IsFalse()
    {
        ExternalAccountAppService.IsTokenFailure(IdentityResult.Success).ShouldBeFalse();
    }

    [Fact]
    public void IsTokenFailure_InvalidToken_IsTrue()
    {
        var result = IdentityResult.Failed(new IdentityError { Code = "InvalidToken" });
        ExternalAccountAppService.IsTokenFailure(result).ShouldBeTrue();
    }

    [Theory]
    [InlineData("PasswordTooShort")]
    [InlineData("PasswordRequiresDigit")]
    [InlineData("PasswordRequiresNonAlphanumeric")]
    public void IsTokenFailure_PolicyErrors_AreFalse(string code)
    {
        // The distinction that matters to the user: a policy failure is fixable
        // on the spot, a token failure means requesting a new email. Treating a
        // policy error as a token failure sends someone back to their inbox for
        // a link that was never the problem.
        var result = IdentityResult.Failed(new IdentityError { Code = code });
        ExternalAccountAppService.IsTokenFailure(result).ShouldBeFalse();
    }

    [Fact]
    public void IsTokenFailure_MixedErrors_FindsInvalidTokenAmongThem()
    {
        // The loop must scan every error, not just the first. ASP.NET Core
        // Identity returns policy failures and token failures together, and
        // checking only Errors[0] would miss the token failure whenever a
        // policy error is listed ahead of it.
        var result = IdentityResult.Failed(
            new IdentityError { Code = "PasswordTooShort" },
            new IdentityError { Code = "InvalidToken" });
        ExternalAccountAppService.IsTokenFailure(result).ShouldBeTrue();
    }

    [Fact]
    public void IsTokenFailure_CodeComparisonIsOrdinal()
    {
        // Identity emits exactly "InvalidToken". A case-insensitive compare
        // would also swallow any future differently-cased code, so the ordinal
        // compare is pinned rather than left to chance.
        var result = IdentityResult.Failed(new IdentityError { Code = "invalidtoken" });
        ExternalAccountAppService.IsTokenFailure(result).ShouldBeFalse();
    }
}
