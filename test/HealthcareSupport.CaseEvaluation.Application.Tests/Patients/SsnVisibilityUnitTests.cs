using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Patients;

/// <summary>
/// F1 / Design B (2026-05-29) -- pure tests for <see cref="SsnVisibility"/>.
/// The helper now masks <see cref="PatientDto.SocialSecurityNumber"/> to the
/// last 4 UNCONDITIONALLY at the AppService mapping boundary (no role /
/// ownership inputs). The full value is served only by the audited reveal
/// endpoint, which does not use this helper.
///
/// Acceptance grid:
///   any value (>= 4 chars)  -> "***-**-LAST4"
///   null / empty SSN        -> unchanged (passthrough)
///   shorter than 4 chars    -> mask-only ("***-**-")
///
/// Synthetic test values are hex strings (per .claude/rules/test-data.md);
/// the helper treats SSN as opaque and just takes the trailing 4 chars.
/// </summary>
public class SsnVisibilityUnitTests
{
    private const string SyntheticSsn = "abcdef789";

    // ---------------- string overload ----------------

    [Fact]
    public void MaskToLast4_AnyValue_ReturnsMaskedLast4()
    {
        var result = SsnVisibility.MaskToLast4(SyntheticSsn);
        result.ShouldBe(SsnVisibility.MaskedPrefix + "f789");
    }

    [Fact]
    public void MaskToLast4_NullSsn_ReturnsNull()
    {
        var result = SsnVisibility.MaskToLast4((string?)null);
        result.ShouldBeNull();
    }

    [Fact]
    public void MaskToLast4_EmptySsn_ReturnsEmpty()
    {
        var result = SsnVisibility.MaskToLast4(string.Empty);
        result.ShouldBe(string.Empty);
    }

    [Fact]
    public void MaskToLast4_ShortSsn_ReturnsMaskOnly()
    {
        var result = SsnVisibility.MaskToLast4("abc");
        result.ShouldBe(SsnVisibility.MaskedPrefix);
    }

    [Fact]
    public void MaskToLast4_ExactlyFourChars_ReturnsMaskPlusValue()
    {
        var result = SsnVisibility.MaskToLast4("abcd");
        result.ShouldBe(SsnVisibility.MaskedPrefix + "abcd");
    }

    // ---------------- PatientDto overload ----------------

    [Fact]
    public void MaskToLast4_PatientDto_MasksSsnInPlace()
    {
        var dto = new PatientDto { SocialSecurityNumber = SyntheticSsn };
        var result = SsnVisibility.MaskToLast4(dto);
        result.ShouldNotBeNull();
        result!.SocialSecurityNumber.ShouldBe(SsnVisibility.MaskedPrefix + "f789");
    }

    [Fact]
    public void MaskToLast4_PatientDto_ReturnsSameInstance()
    {
        var dto = new PatientDto { SocialSecurityNumber = SyntheticSsn };
        var result = SsnVisibility.MaskToLast4(dto);
        ReferenceEquals(dto, result).ShouldBeTrue();
    }

    [Fact]
    public void MaskToLast4_PatientDto_Null_ReturnsNull()
    {
        var result = SsnVisibility.MaskToLast4((PatientDto?)null);
        result.ShouldBeNull();
    }

    // ---------------- PatientWithNavigationPropertiesDto overload ----------------

    [Fact]
    public void MaskToLast4_WithNav_MasksWrappedPatient()
    {
        var dto = new PatientWithNavigationPropertiesDto
        {
            Patient = new PatientDto { SocialSecurityNumber = SyntheticSsn },
        };
        var result = SsnVisibility.MaskToLast4(dto);
        result.ShouldNotBeNull();
        result!.Patient!.SocialSecurityNumber.ShouldBe(SsnVisibility.MaskedPrefix + "f789");
    }

    [Fact]
    public void MaskToLast4_WithNav_NullPatient_NoOp()
    {
        // PatientWithNavigationPropertiesDto.Patient is declared non-nullable
        // with a `null!` default; treat that runtime-null shape as the
        // "no Patient mapped" edge case.
        var dto = new PatientWithNavigationPropertiesDto { Patient = null! };
        var result = SsnVisibility.MaskToLast4(dto);
        result.ShouldNotBeNull();
        result!.Patient.ShouldBeNull();
    }

    [Fact]
    public void MaskToLast4_WithNav_Null_ReturnsNull()
    {
        var result = SsnVisibility.MaskToLast4((PatientWithNavigationPropertiesDto?)null);
        result.ShouldBeNull();
    }
}
