using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Emailing;

/// <summary>
/// Phase 4.1 (2026-09-09) -- pure tests for <see cref="EmailAddressVisibility"/>, the masker that
/// closes CodeQL alert 211: a full email address reaching a Warning-level log through the
/// emailer's context tag.
///
/// <para>Acceptance grid, all decided rather than discovered:</para>
/// <code>
/// "adrian@example.test"   -> "a***@example.test"     first local char kept, domain kept
/// "a@example.test"        -> "a***@example.test"     single-char local: same shape
/// "@example.test"         -> "***@example.test"      no local char to keep
/// "not-an-address"        -> "***"                   no '@': nothing is safe to keep
/// "a@b@example.test"      -> "a***@example.test"     the LAST '@' delimits the domain
/// null                    -> null                    passthrough
/// ""                      -> ""                      passthrough
/// </code>
///
/// <para><b>The domain is retained DELIBERATELY.</b> Adrian chose "mask the address, keep partial
/// correlation" over dropping the tag entirely, with this exact shape in front of him. Retaining
/// the domain is what preserves the tag's diagnostic value -- it says which emailer path failed
/// and roughly for whom -- and it is the reason the option was chosen. <b>Note the trade-off so
/// nobody "tightens" it without knowing it was considered:</b> on a small tenant a domain can
/// narrow identity. That was weighed and accepted; changing it is a new decision, not a cleanup.</para>
///
/// <para>Mirrors <c>SsnVisibility</c> -- internal static, pure, no DI, a const mask, null in /
/// null out, and a fallback for input too short to mask meaningfully. Synthetic values only, per
/// <c>.claude/rules/test-data.md</c>.</para>
/// </summary>
public class EmailAddressVisibilityUnitTests
{
    [Fact]
    public void Mask_KeepsTheFirstLocalCharacterAndTheDomain()
    {
        EmailAddressVisibility.Mask("adrian@example.test").ShouldBe("a***@example.test");
    }

    [Fact]
    public void Mask_SingleCharacterLocalPart_ProducesTheSameShape()
    {
        // The first character IS the whole local part here, so nothing is revealed that the
        // general case would not reveal. Called out because it is the case most likely to be
        // "fixed" by someone who thinks it leaks more than the others.
        EmailAddressVisibility.Mask("a@example.test").ShouldBe("a***@example.test");
    }

    [Fact]
    public void Mask_EmptyLocalPart_KeepsNoLeadingCharacter()
    {
        EmailAddressVisibility.Mask("@example.test").ShouldBe("***@example.test");
    }

    [Fact]
    public void Mask_WithoutAnAtSign_RevealsNothing()
    {
        // Not an address, so there is no domain worth keeping and no way to know what the value
        // is. Fail closed: emit the mask alone rather than guessing which part is safe.
        EmailAddressVisibility.Mask("not-an-address").ShouldBe("***");
    }

    [Fact]
    public void Mask_MultipleAtSigns_TreatsTheLastAsTheDomainDelimiter()
    {
        // A quoted local part may legally contain '@'; the domain is whatever follows the LAST
        // one. Splitting on the first would leak the rest of the local part into the "domain".
        EmailAddressVisibility.Mask("a@b@example.test").ShouldBe("a***@example.test");
    }

    [Fact]
    public void Mask_Null_IsPassedThroughUnchanged()
    {
        EmailAddressVisibility.Mask(null).ShouldBeNull();
    }

    [Fact]
    public void Mask_Empty_IsPassedThroughUnchanged()
    {
        EmailAddressVisibility.Mask(string.Empty).ShouldBe(string.Empty);
    }

    [Fact]
    public void Mask_NeverReturnsAValueContainingTheOriginalLocalPart()
    {
        // The property that actually matters, asserted independently of the exact shape above so
        // a future formatting change cannot quietly reintroduce the identifier.
        const string local = "averyspecificlocalpart";
        var masked = EmailAddressVisibility.Mask(local + "@example.test");

        masked.ShouldNotBeNull();
        masked!.ShouldNotContain(local);
        masked.ShouldNotContain(local.Substring(1));
    }
}
