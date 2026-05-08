using System;
using HealthcareSupport.CaseEvaluation.ExternalSignups;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.ExternalSignups;

/// <summary>
/// Phase 8 (2026-05-03) -- pure unit tests for the registration validator
/// + helpers in <see cref="ExternalSignupAppService"/>. Bypasses ABP's
/// integration test harness (currently blocked by the ABP Pro license
/// gate per <c>docs/handoffs/2026-05-03-test-host-license-blocker.md</c>)
/// by exercising the <c>internal static</c> helpers via the
/// <c>InternalsVisibleTo</c> hook in the Application assembly's
/// <c>AssemblyInfo.cs</c>.
///
/// Coverage:
/// <list type="bullet">
///   <item>OLD parity: ConfirmPassword match enforced (audit G2).</item>
///   <item>OLD parity: FirmName required for ApplicantAttorney AND
///         DefenseAttorney (audit G6 OLD-bug-fix).</item>
///   <item>OLD parity: FirmEmail auto-derived from Email when not
///         supplied (UserDomain.cs:106).</item>
///   <item>OLD parity: Adjuster role mapped to "Adjuster" (audit G1).</item>
///   <item>Negative cases: null DTO, blank email/password.</item>
/// </list>
/// </summary>
public class ExternalSignupValidatorUnitTests
{
    // ------------------------------------------------------------------
    // ValidateRegistrationInput -- null + required
    // ------------------------------------------------------------------

    [Fact]
    public void ValidateRegistrationInput_NullInput_Throws()
    {
        Should.Throw<ArgumentException>(
            () => ExternalSignupAppService.ValidateRegistrationInput(null!));
    }

    [Fact]
    public void ValidateRegistrationInput_EmptyEmail_Throws()
    {
        var dto = ValidPatientDto();
        dto.Email = "";

        Should.Throw<ArgumentException>(
            () => ExternalSignupAppService.ValidateRegistrationInput(dto));
    }

    [Fact]
    public void ValidateRegistrationInput_EmptyPassword_Throws()
    {
        var dto = ValidPatientDto();
        dto.Password = "";

        Should.Throw<ArgumentException>(
            () => ExternalSignupAppService.ValidateRegistrationInput(dto));
    }

    [Fact]
    public void ValidateRegistrationInput_EmptyConfirmPassword_Throws()
    {
        var dto = ValidPatientDto();
        dto.ConfirmPassword = "";

        Should.Throw<ArgumentException>(
            () => ExternalSignupAppService.ValidateRegistrationInput(dto));
    }

    // ------------------------------------------------------------------
    // ValidateRegistrationInput -- ConfirmPassword (G2)
    // ------------------------------------------------------------------

    [Fact]
    public void ValidateRegistrationInput_PasswordMismatch_ThrowsBusinessException()
    {
        var dto = ValidPatientDto();
        dto.Password = "Test1234!";
        dto.ConfirmPassword = "Mismatch9!";

        var ex = Should.Throw<BusinessException>(
            () => ExternalSignupAppService.ValidateRegistrationInput(dto));
        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.RegistrationConfirmPasswordMismatch);
    }

    [Fact]
    public void ValidateRegistrationInput_PasswordCaseSensitivity_DistinguishesValues()
    {
        // Ordinal compare: "Test123!" and "test123!" are different.
        var dto = ValidPatientDto();
        dto.Password = "Test123!";
        dto.ConfirmPassword = "test123!";

        Should.Throw<BusinessException>(
            () => ExternalSignupAppService.ValidateRegistrationInput(dto));
    }

    [Fact]
    public void ValidateRegistrationInput_PasswordsMatch_DoesNotThrow()
    {
        var dto = ValidPatientDto();
        ExternalSignupAppService.ValidateRegistrationInput(dto);
    }

    // ------------------------------------------------------------------
    // ValidateRegistrationInput -- FirmName for attorneys (G6 OLD-bug-fix)
    // ------------------------------------------------------------------

    [Fact]
    public void ValidateRegistrationInput_PatientWithoutFirmName_DoesNotThrow()
    {
        var dto = ValidPatientDto();
        dto.FirmName = null;

        ExternalSignupAppService.ValidateRegistrationInput(dto);
    }

    [Fact]
    public void ValidateRegistrationInput_AdjusterWithoutFirmName_DoesNotThrow()
    {
        var dto = ValidPatientDto();
        dto.UserType = ExternalUserType.Adjuster;
        dto.FirmName = null;

        ExternalSignupAppService.ValidateRegistrationInput(dto);
    }

    [Fact]
    public void ValidateRegistrationInput_ApplicantAttorneyWithoutFirmName_Throws()
    {
        var dto = ValidPatientDto();
        dto.UserType = ExternalUserType.ApplicantAttorney;
        dto.FirmName = null;

        var ex = Should.Throw<BusinessException>(
            () => ExternalSignupAppService.ValidateRegistrationInput(dto));
        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.RegistrationFirmNameRequired);
    }

    [Fact]
    public void ValidateRegistrationInput_DefenseAttorneyWithoutFirmName_Throws()
    {
        // OLD-bug-fix: OLD UserDomain.cs:272 checked PatientAttorney twice;
        // DefenseAttorney never had its FirmName validated. NEW fixes this
        // by validating both attorney roles.
        var dto = ValidPatientDto();
        dto.UserType = ExternalUserType.DefenseAttorney;
        dto.FirmName = null;

        var ex = Should.Throw<BusinessException>(
            () => ExternalSignupAppService.ValidateRegistrationInput(dto));
        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.RegistrationFirmNameRequired);
    }

    [Fact]
    public void ValidateRegistrationInput_ApplicantAttorneyEmptyFirmName_Throws()
    {
        var dto = ValidPatientDto();
        dto.UserType = ExternalUserType.ApplicantAttorney;
        dto.FirmName = "   ";

        Should.Throw<BusinessException>(
            () => ExternalSignupAppService.ValidateRegistrationInput(dto));
    }

    [Fact]
    public void ValidateRegistrationInput_DefenseAttorneyWithFirmName_DoesNotThrow()
    {
        var dto = ValidPatientDto();
        dto.UserType = ExternalUserType.DefenseAttorney;
        dto.FirmName = "Smith & Co";

        ExternalSignupAppService.ValidateRegistrationInput(dto);
    }

    // ------------------------------------------------------------------
    // IsAttorneyRole
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(ExternalUserType.ApplicantAttorney, true)]
    [InlineData(ExternalUserType.DefenseAttorney, true)]
    [InlineData(ExternalUserType.Patient, false)]
    [InlineData(ExternalUserType.Adjuster, false)]
    [InlineData(ExternalUserType.ClaimExaminer, false)]
    public void IsAttorneyRole_RecognizesBothAttorneyEnums(ExternalUserType userType, bool expected)
    {
        ExternalSignupAppService.IsAttorneyRole(userType).ShouldBe(expected);
    }

    // ------------------------------------------------------------------
    // ToRoleName -- Adjuster mapping (G1)
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(ExternalUserType.Patient, "Patient")]
    [InlineData(ExternalUserType.Adjuster, "Adjuster")]
    [InlineData(ExternalUserType.ClaimExaminer, "Claim Examiner")]
    [InlineData(ExternalUserType.ApplicantAttorney, "Applicant Attorney")]
    [InlineData(ExternalUserType.DefenseAttorney, "Defense Attorney")]
    public void ToRoleName_MapsKnownTypes(ExternalUserType userType, string expected)
    {
        ExternalSignupAppService.ToRoleName(userType).ShouldBe(expected);
    }

    // ------------------------------------------------------------------
    // DeriveFirmEmail -- OLD UserDomain.cs:106 parity
    // ------------------------------------------------------------------

    [Fact]
    public void DeriveFirmEmail_NoExplicitFirmEmail_DerivesFromEmailLowercase()
    {
        // OLD UserDomain.cs:106:
        //   user.FirmEmail = user.EmailId.ToLower().ToString().Trim();
        var dto = ValidPatientDto();
        dto.UserType = ExternalUserType.ApplicantAttorney;
        dto.Email = "Counsel@LawFirm.COM";
        dto.FirmEmail = null;
        dto.FirmName = "Law Firm";

        ExternalSignupAppService.DeriveFirmEmail(dto).ShouldBe("counsel@lawfirm.com");
    }

    [Fact]
    public void DeriveFirmEmail_BlankExplicitFirmEmail_FallsBackToEmail()
    {
        var dto = ValidPatientDto();
        dto.UserType = ExternalUserType.ApplicantAttorney;
        dto.Email = "Lead@Firm.io";
        dto.FirmEmail = "   ";
        dto.FirmName = "Firm";

        ExternalSignupAppService.DeriveFirmEmail(dto).ShouldBe("lead@firm.io");
    }

    [Fact]
    public void DeriveFirmEmail_ExplicitFirmEmail_LowercasesAndReturnsProvided()
    {
        var dto = ValidPatientDto();
        dto.UserType = ExternalUserType.ApplicantAttorney;
        dto.Email = "Counsel@LawFirm.COM";
        dto.FirmEmail = "Reception@LawFirm.com";
        dto.FirmName = "Law Firm";

        ExternalSignupAppService.DeriveFirmEmail(dto).ShouldBe("reception@lawfirm.com");
    }

    [Fact]
    public void DeriveFirmEmail_TrimsWhitespace()
    {
        var dto = ValidPatientDto();
        dto.UserType = ExternalUserType.ApplicantAttorney;
        dto.Email = "  Counsel@LawFirm.com  ";
        dto.FirmEmail = null;
        dto.FirmName = "Law Firm";

        ExternalSignupAppService.DeriveFirmEmail(dto).ShouldBe("counsel@lawfirm.com");
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static ExternalUserSignUpDto ValidPatientDto() => new()
    {
        UserType = ExternalUserType.Patient,
        Email = "patient@example.test",
        Password = "Test1234!",
        ConfirmPassword = "Test1234!",
        FirstName = "Pat",
        LastName = "Tester",
        TenantId = Guid.NewGuid(),
    };
}
