using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Unit tests for claim-identifier normalisation. Claim numbers and WCAB ADJ numbers are free text in
/// the portal -- validated only for required and 50 characters, with no pattern, trim or case rule -- so
/// two bookings of the SAME claim can differ by punctuation or case. The Case Tracker groups records by
/// claim, so without a normalised form it cannot match them, which is the misfiling problem this whole
/// change exists to fix.
///
/// <para>The equivalence cases below are the receiver's own example: WC-4417 and WC4417.</para>
/// </summary>
public class ClaimIdentifierNormalizerTests
{
    [Theory]
    [InlineData("WC-4417")]
    [InlineData("WC4417")]
    [InlineData("wc4417")]
    [InlineData("wc-4417")]
    [InlineData("WC 4417")]
    [InlineData("  WC-4417  ")]
    [InlineData("W.C./4417")]
    public void PunctuationAndCaseVariants_AllCollapseToOneValue(string input)
    {
        ClaimIdentifierNormalizer.Normalize(input).ShouldBe("WC4417");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankInput_NormalizesToNull(string? input)
    {
        // Null rather than empty string, so the wire carries an explicit "nothing to group on".
        ClaimIdentifierNormalizer.Normalize(input).ShouldBeNull();
    }

    [Fact]
    public void PunctuationOnlyInput_NormalizesToNull()
    {
        // Nothing alphanumeric survives, so there is no key -- do not emit an empty string that would
        // make two meaningless values compare equal.
        ClaimIdentifierNormalizer.Normalize("---").ShouldBeNull();
    }

    [Fact]
    public void GenuinelyDifferentIdentifiers_StayDifferent()
    {
        ClaimIdentifierNormalizer.Normalize("WC-4417")
            .ShouldNotBe(ClaimIdentifierNormalizer.Normalize("WC-4418"));
    }

    [Fact]
    public void TheCallersStringIsNotAltered()
    {
        const string original = "wc-4417";

        ClaimIdentifierNormalizer.Normalize(original);

        original.ShouldBe("wc-4417");
    }

    [Fact]
    public void AdjNumbersNormalizeTheSameWay()
    {
        // Same helper serves wcabAdj; the receiver needs both groupable.
        ClaimIdentifierNormalizer.Normalize("ADJ 99-30").ShouldBe("ADJ9930");
    }
}
