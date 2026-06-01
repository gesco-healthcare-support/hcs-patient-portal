using System;
using System.Collections.Generic;
using HealthcareSupport.CaseEvaluation.ExternalSignups;
using HealthcareSupport.CaseEvaluation.Localization;
using Microsoft.Extensions.Localization;
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
///   <item>ToRoleName maps each external role to its seeded role-name string.</item>
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
    public void ValidateRegistrationInput_PasswordMismatch_ThrowsUserFriendlyException()
    {
        // BUG-012 (2026-05-22): UFE is the now-required type so the
        // localized message reaches the SPA banner. UFE inherits from
        // BusinessException, so older Should.Throw<BusinessException>
        // call sites would still pass; the stricter assertion here
        // guards against accidental regression to a plain BusinessException
        // (whose message would get suppressed to "An internal error...").
        var dto = ValidPatientDto();
        dto.Password = "Test1234!";
        dto.ConfirmPassword = "Mismatch9!";

        var ex = Should.Throw<UserFriendlyException>(
            () => ExternalSignupAppService.ValidateRegistrationInput(dto));
        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.RegistrationConfirmPasswordMismatch);
        ex.Message.ShouldBe("Password and confirm password do not match.");
    }

    [Fact]
    public void ValidateRegistrationInput_PasswordCaseSensitivity_DistinguishesValues()
    {
        // Ordinal compare: "Test123!" and "test123!" are different.
        var dto = ValidPatientDto();
        dto.Password = "Test123!";
        dto.ConfirmPassword = "test123!";

        Should.Throw<UserFriendlyException>(
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
    public void ValidateRegistrationInput_ClaimExaminerWithoutFirmName_DoesNotThrow()
    {
        var dto = ValidPatientDto();
        dto.UserType = ExternalUserType.ClaimExaminer;
        dto.FirmName = null;

        ExternalSignupAppService.ValidateRegistrationInput(dto);
    }

    [Fact]
    public void ValidateRegistrationInput_ApplicantAttorneyWithoutFirmName_Throws()
    {
        var dto = ValidPatientDto();
        dto.UserType = ExternalUserType.ApplicantAttorney;
        dto.FirmName = null;

        var ex = Should.Throw<UserFriendlyException>(
            () => ExternalSignupAppService.ValidateRegistrationInput(dto));
        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.RegistrationFirmNameRequired);
        // BUG-012 (2026-05-22): assert the English fallback string reaches
        // the consumer (null localizer path). The localized-path coverage
        // is in ValidateRegistrationInput_AttorneyWithoutFirmName_LocalizerOverridesMessage.
        ex.Message.ShouldBe("Firm name is required for attorney roles.");
        ex.Data["UserType"].ShouldBe("ApplicantAttorney");
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

        var ex = Should.Throw<UserFriendlyException>(
            () => ExternalSignupAppService.ValidateRegistrationInput(dto));
        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.RegistrationFirmNameRequired);
        ex.Data["UserType"].ShouldBe("DefenseAttorney");
    }

    [Fact]
    public void ValidateRegistrationInput_ApplicantAttorneyEmptyFirmName_Throws()
    {
        var dto = ValidPatientDto();
        dto.UserType = ExternalUserType.ApplicantAttorney;
        dto.FirmName = "   ";

        Should.Throw<UserFriendlyException>(
            () => ExternalSignupAppService.ValidateRegistrationInput(dto));
    }

    [Fact]
    public void ValidateRegistrationInput_AttorneyWithoutFirmName_LocalizerOverridesMessage()
    {
        // BUG-012 (2026-05-22): verify the optional localizer parameter
        // path produces the localized string instead of the English
        // fallback. Production caller (RegisterAsync) passes the
        // CaseEvaluationResource-bound localizer so the SPA banner shows
        // the en.json value "Firm name is required for attorney roles."
        // rather than the hardcoded fallback. A fake localizer below
        // returns a sentinel string so we can prove the localizer code
        // path was taken.
        var dto = ValidPatientDto();
        dto.UserType = ExternalUserType.ApplicantAttorney;
        dto.FirmName = null;
        var fake = new FakeFirmNameLocalizer("LOCALIZED-FIRM-MSG");

        var ex = Should.Throw<UserFriendlyException>(
            () => ExternalSignupAppService.ValidateRegistrationInput(dto, fake));

        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.RegistrationFirmNameRequired);
        ex.Message.ShouldBe("LOCALIZED-FIRM-MSG");
    }

    [Fact]
    public void ValidateRegistrationInput_PasswordMismatch_LocalizerOverridesMessage()
    {
        // BUG-012 (2026-05-22): same localizer-override path for the
        // ConfirmPasswordMismatch branch -- caller path proves the
        // confirm-password message also reaches the SPA verbatim.
        var dto = ValidPatientDto();
        dto.Password = "Test1234!";
        dto.ConfirmPassword = "Mismatch9!";
        var fake = new FakeConfirmPasswordLocalizer("LOCALIZED-PW-MSG");

        var ex = Should.Throw<UserFriendlyException>(
            () => ExternalSignupAppService.ValidateRegistrationInput(dto, fake));

        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.RegistrationConfirmPasswordMismatch);
        ex.Message.ShouldBe("LOCALIZED-PW-MSG");
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
    [InlineData(ExternalUserType.ClaimExaminer, false)]
    public void IsAttorneyRole_RecognizesBothAttorneyEnums(ExternalUserType userType, bool expected)
    {
        ExternalSignupAppService.IsAttorneyRole(userType).ShouldBe(expected);
    }

    // ------------------------------------------------------------------
    // ToRoleName -- role-name mapping
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(ExternalUserType.Patient, "Patient")]
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

    // BUG-012 (2026-05-22): minimal IStringLocalizer<CaseEvaluationResource>
    // stubs so the validator's localizer-driven message path can be exercised
    // without standing up ABP's IStringLocalizer DI chain. Each stub returns
    // the configured sentinel for ITS key and an unlocalized passthrough
    // (ResourceNotFound = true) for any other lookup -- mirroring the real
    // ABP localizer's behavior when a key is missing.
    private sealed class FakeFirmNameLocalizer : FakeLocalizerBase
    {
        public FakeFirmNameLocalizer(string sentinel)
            : base("Registration:FirmNameRequiredForAttorney", sentinel) { }
    }

    private sealed class FakeConfirmPasswordLocalizer : FakeLocalizerBase
    {
        public FakeConfirmPasswordLocalizer(string sentinel)
            : base("Registration:ConfirmPasswordMismatch", sentinel) { }
    }

    private abstract class FakeLocalizerBase : IStringLocalizer<CaseEvaluationResource>
    {
        private readonly string _key;
        private readonly string _sentinel;

        protected FakeLocalizerBase(string key, string sentinel)
        {
            _key = key;
            _sentinel = sentinel;
        }

        public LocalizedString this[string name] =>
            name == _key
                ? new LocalizedString(name, _sentinel, resourceNotFound: false)
                : new LocalizedString(name, name, resourceNotFound: true);

        public LocalizedString this[string name, params object[] arguments] => this[name];

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) =>
            Array.Empty<LocalizedString>();
    }
}
