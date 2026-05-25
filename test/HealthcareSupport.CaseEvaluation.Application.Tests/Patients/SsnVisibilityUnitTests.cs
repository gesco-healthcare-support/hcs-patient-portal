using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Patients;

/// <summary>
/// F4-01 (2026-05-25) -- pure tests for <see cref="SsnVisibility"/>.
/// The helper redacts <see cref="PatientDto.SocialSecurityNumber"/>
/// at the AppService mapping boundary based on caller role and
/// record ownership.
///
/// Acceptance grid:
///   internal role           -> full value
///   record owner            -> full value (even if external role)
///   external non-owner      -> "***-**-LAST4"
///   null / empty SSN        -> unchanged
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
    public void RedactForCaller_InternalCaller_KeepsFullSsn()
    {
        var result = SsnVisibility.RedactForCaller(SyntheticSsn, isInternalCaller: true, isRecordOwner: false);
        result.ShouldBe(SyntheticSsn);
    }

    [Fact]
    public void RedactForCaller_RecordOwner_KeepsFullSsnEvenWhenExternal()
    {
        var result = SsnVisibility.RedactForCaller(SyntheticSsn, isInternalCaller: false, isRecordOwner: true);
        result.ShouldBe(SyntheticSsn);
    }

    [Fact]
    public void RedactForCaller_ExternalNonOwner_ReturnsMaskedLast4()
    {
        var result = SsnVisibility.RedactForCaller(SyntheticSsn, isInternalCaller: false, isRecordOwner: false);
        result.ShouldBe(SsnVisibility.MaskedPrefix + "f789");
    }

    [Fact]
    public void RedactForCaller_NullSsn_ReturnsNull()
    {
        var result = SsnVisibility.RedactForCaller((string?)null, isInternalCaller: false, isRecordOwner: false);
        result.ShouldBeNull();
    }

    [Fact]
    public void RedactForCaller_EmptySsn_ReturnsEmpty()
    {
        var result = SsnVisibility.RedactForCaller(string.Empty, isInternalCaller: false, isRecordOwner: false);
        result.ShouldBe(string.Empty);
    }

    [Fact]
    public void RedactForCaller_ShortSsn_ReturnsMaskOnly()
    {
        var result = SsnVisibility.RedactForCaller("abc", isInternalCaller: false, isRecordOwner: false);
        result.ShouldBe(SsnVisibility.MaskedPrefix);
    }

    [Fact]
    public void RedactForCaller_ExactlyFourChars_ReturnsMaskPlusValue()
    {
        var result = SsnVisibility.RedactForCaller("abcd", isInternalCaller: false, isRecordOwner: false);
        result.ShouldBe(SsnVisibility.MaskedPrefix + "abcd");
    }

    // ---------------- PatientDto overload ----------------

    [Fact]
    public void RedactForCaller_PatientDto_ExternalNonOwner_MasksSsnInPlace()
    {
        var dto = new PatientDto { SocialSecurityNumber = SyntheticSsn };
        var result = SsnVisibility.RedactForCaller(dto, isInternalCaller: false, isRecordOwner: false);
        result.ShouldNotBeNull();
        result!.SocialSecurityNumber.ShouldBe(SsnVisibility.MaskedPrefix + "f789");
    }

    [Fact]
    public void RedactForCaller_PatientDto_InternalCaller_LeavesSsnIntact()
    {
        var dto = new PatientDto { SocialSecurityNumber = SyntheticSsn };
        var result = SsnVisibility.RedactForCaller(dto, isInternalCaller: true, isRecordOwner: false);
        result.ShouldNotBeNull();
        result!.SocialSecurityNumber.ShouldBe(SyntheticSsn);
    }

    [Fact]
    public void RedactForCaller_PatientDto_ReturnsSameInstance()
    {
        var dto = new PatientDto { SocialSecurityNumber = SyntheticSsn };
        var result = SsnVisibility.RedactForCaller(dto, isInternalCaller: false, isRecordOwner: false);
        ReferenceEquals(dto, result).ShouldBeTrue();
    }

    [Fact]
    public void RedactForCaller_PatientDto_Null_ReturnsNull()
    {
        var result = SsnVisibility.RedactForCaller((PatientDto?)null, isInternalCaller: false, isRecordOwner: false);
        result.ShouldBeNull();
    }

    // ---------------- PatientWithNavigationPropertiesDto overload ----------------

    [Fact]
    public void RedactForCaller_WithNav_ExternalNonOwner_MasksWrappedPatient()
    {
        var dto = new PatientWithNavigationPropertiesDto
        {
            Patient = new PatientDto { SocialSecurityNumber = SyntheticSsn },
        };
        var result = SsnVisibility.RedactForCaller(dto, isInternalCaller: false, isRecordOwner: false);
        result.ShouldNotBeNull();
        result!.Patient!.SocialSecurityNumber.ShouldBe(SsnVisibility.MaskedPrefix + "f789");
    }

    [Fact]
    public void RedactForCaller_WithNav_NullPatient_NoOp()
    {
        // PatientWithNavigationPropertiesDto.Patient is declared non-nullable
        // with a `null!` default; treat that runtime-null shape as the
        // "no Patient mapped" edge case.
        var dto = new PatientWithNavigationPropertiesDto { Patient = null! };
        var result = SsnVisibility.RedactForCaller(dto, isInternalCaller: false, isRecordOwner: false);
        result.ShouldNotBeNull();
        result!.Patient.ShouldBeNull();
    }

    [Fact]
    public void RedactForCaller_WithNav_Null_ReturnsNull()
    {
        var result = SsnVisibility.RedactForCaller((PatientWithNavigationPropertiesDto?)null, isInternalCaller: false, isRecordOwner: false);
        result.ShouldBeNull();
    }
}
