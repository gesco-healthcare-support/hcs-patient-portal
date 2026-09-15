using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.ExternalSignups;

/// <summary>
/// Phase 1 / C2 / D4 (firm-based AA/DA registration) -- pure tests for
/// <see cref="ExternalUserDisplayName"/>.
///
/// Acceptance grid (precedence locked by Adrian Q1 2026-06-11):
///   First + Last present     -> "First Last"
///   first-only / last-only   -> the present name part
///   names blank, firm present -> FirmName        (the firm-account case)
///   names + firm blank        -> Email
///   everything blank          -> ""              (never null)
///
/// Synthetic values only (per .claude/rules/test-data.md).
/// </summary>
public class ExternalUserDisplayNameUnitTests
{
    [Fact]
    public void Resolve_FullName_ReturnsFirstSpaceLast()
    {
        ExternalUserDisplayName.Resolve("Avery", "Tester", "Firm LLP", "a@example.com")
            .ShouldBe("Avery Tester");
    }

    [Fact]
    public void Resolve_FirstNameOnly_ReturnsFirst()
    {
        ExternalUserDisplayName.Resolve("Avery", null, "Firm LLP", "a@example.com")
            .ShouldBe("Avery");
    }

    [Fact]
    public void Resolve_LastNameOnly_ReturnsLast()
    {
        ExternalUserDisplayName.Resolve(null, "Tester", "Firm LLP", "a@example.com")
            .ShouldBe("Tester");
    }

    [Fact]
    public void Resolve_NamesBlank_FirmPresent_ReturnsFirm()
    {
        ExternalUserDisplayName.Resolve(null, null, "Firm LLP", "a@example.com")
            .ShouldBe("Firm LLP");
    }

    [Fact]
    public void Resolve_NamesWhitespace_FirmPresent_ReturnsFirm()
    {
        ExternalUserDisplayName.Resolve("   ", "  ", "Firm LLP", "a@example.com")
            .ShouldBe("Firm LLP");
    }

    [Fact]
    public void Resolve_NamesAndFirmBlank_ReturnsEmail()
    {
        ExternalUserDisplayName.Resolve(null, null, "   ", "a@example.com")
            .ShouldBe("a@example.com");
    }

    [Fact]
    public void Resolve_EverythingBlank_ReturnsEmptyNotNull()
    {
        ExternalUserDisplayName.Resolve(null, null, null, null)
            .ShouldBe(string.Empty);
    }

    [Fact]
    public void Resolve_TrimsSurroundingWhitespace()
    {
        ExternalUserDisplayName.Resolve("  Avery  ", "  Tester  ", null, null)
            .ShouldBe("Avery Tester");
        ExternalUserDisplayName.Resolve(null, null, null, "  a@example.com  ")
            .ShouldBe("a@example.com");
    }
}
