using HealthcareSupport.CaseEvaluation.Notifications;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Notifications;

/// <summary>
/// Phase 14b (2026-05-04) -- pure unit tests for the
/// <see cref="EmailSubjectBuilder"/> identity-suffix builder.
/// Pins the OLD-verbatim format
/// "(Patient: {first} {last} - Claim: {claim} - ADJ: {adj})" against
/// every realistic combination of empty / null / partial inputs.
/// </summary>
public class EmailSubjectBuilderUnitTests
{
    [Fact]
    public void BuildIdentitySuffix_AllFieldsSupplied_FullFormat()
    {
        EmailSubjectBuilder
            .BuildIdentitySuffix("Jane", "Doe", "WC-12345", "ADJ-9999")
            .ShouldBe("(Patient: Jane Doe - Claim: WC-12345 - ADJ: ADJ-9999)");
    }

    [Fact]
    public void BuildIdentitySuffix_OnlyName_NameOnly()
    {
        EmailSubjectBuilder
            .BuildIdentitySuffix("Jane", "Doe", null, null)
            .ShouldBe("(Patient: Jane Doe)");
    }

    [Fact]
    public void BuildIdentitySuffix_OnlyClaim_ClaimOnly()
    {
        EmailSubjectBuilder
            .BuildIdentitySuffix(null, null, "WC-12345", null)
            .ShouldBe("(Claim: WC-12345)");
    }

    [Fact]
    public void BuildIdentitySuffix_OnlyAdj_AdjOnly()
    {
        EmailSubjectBuilder
            .BuildIdentitySuffix(null, null, null, "ADJ-9999")
            .ShouldBe("(ADJ: ADJ-9999)");
    }

    [Fact]
    public void BuildIdentitySuffix_NameAndClaim_NoAdj()
    {
        EmailSubjectBuilder
            .BuildIdentitySuffix("Jane", "Doe", "WC-12345", null)
            .ShouldBe("(Patient: Jane Doe - Claim: WC-12345)");
    }

    [Fact]
    public void BuildIdentitySuffix_NameAndAdj_NoClaim()
    {
        EmailSubjectBuilder
            .BuildIdentitySuffix("Jane", "Doe", null, "ADJ-9999")
            .ShouldBe("(Patient: Jane Doe - ADJ: ADJ-9999)");
    }

    [Fact]
    public void BuildIdentitySuffix_ClaimAndAdj_NoName()
    {
        EmailSubjectBuilder
            .BuildIdentitySuffix(null, null, "WC-12345", "ADJ-9999")
            .ShouldBe("(Claim: WC-12345 - ADJ: ADJ-9999)");
    }

    [Fact]
    public void BuildIdentitySuffix_OnlyFirstName_NoLastName()
    {
        EmailSubjectBuilder
            .BuildIdentitySuffix("Jane", null, null, null)
            .ShouldBe("(Patient: Jane)");
    }

    [Fact]
    public void BuildIdentitySuffix_OnlyLastName_NoFirstName()
    {
        EmailSubjectBuilder
            .BuildIdentitySuffix(null, "Doe", null, null)
            .ShouldBe("(Patient: Doe)");
    }

    [Theory]
    [InlineData(null, null, null, null)]
    [InlineData("", "", "", "")]
    [InlineData("   ", "   ", "   ", "   ")]
    public void BuildIdentitySuffix_AllNullOrWhitespace_ReturnsEmptyString(
        string? f, string? l, string? c, string? a)
    {
        EmailSubjectBuilder.BuildIdentitySuffix(f, l, c, a).ShouldBe(string.Empty);
    }

    [Fact]
    public void BuildIdentitySuffix_TrimsWhitespace()
    {
        EmailSubjectBuilder
            .BuildIdentitySuffix("  Jane  ", "  Doe  ", "  WC-12345  ", "  ADJ-9999  ")
            .ShouldBe("(Patient: Jane Doe - Claim: WC-12345 - ADJ: ADJ-9999)");
    }

    [Fact]
    public void BuildIdentitySuffixFromFullName_AllSupplied_NoLastNameOverride()
    {
        EmailSubjectBuilder
            .BuildIdentitySuffixFromFullName("Jane Q. Doe", "WC-12345", "ADJ-9999")
            .ShouldBe("(Patient: Jane Q. Doe - Claim: WC-12345 - ADJ: ADJ-9999)");
    }

    [Fact]
    public void BuildIdentitySuffix_PartialWhitespaceCollapsedAround_Name()
    {
        // First-only with whitespace last -- still treat as first only.
        EmailSubjectBuilder
            .BuildIdentitySuffix("Jane", "  ", "WC-1", null)
            .ShouldBe("(Patient: Jane - Claim: WC-1)");
    }
}
