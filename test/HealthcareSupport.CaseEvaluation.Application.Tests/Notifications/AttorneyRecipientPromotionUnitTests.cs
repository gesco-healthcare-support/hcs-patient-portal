using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Notifications;

/// <summary>
/// Phase 4 / C3 / D3 (firm-based AA/DA registration) -- pure tests for
/// <see cref="AttorneyRecipientPromotion.ResolvePrimaryRecipientEmail"/>.
///
/// Promotion grid (keyed off the appointment CREATOR's roles, Phase-0 correction):
///   creator holds Applicant Attorney + AA email named -> AA email
///   creator holds Defense Attorney  + DA email named  -> DA email
///   creator holds both (D9)                            -> AA email (precedence)
///   role held but that side's email blank              -> null (no promotion)
///   Patient / Claim Examiner / internal-staff creator  -> null (no promotion)
///
/// Returning null when not promoted is load-bearing: callers anchor on
/// "PrimaryRecipientEmail ?? bookerEmail", so a null keeps non-promoted bookings
/// (incl. internal-staff-books-a-patient) byte-identical.
///
/// Synthetic values only (per .claude/rules/test-data.md).
/// </summary>
public class AttorneyRecipientPromotionUnitTests
{
    private const string Aa = "Applicant Attorney";
    private const string Da = "Defense Attorney";

    [Fact]
    public void ApplicantAttorneyCreator_WithAaEmail_PromotesAaEmail()
    {
        AttorneyRecipientPromotion.ResolvePrimaryRecipientEmail(
                new[] { Aa }, "aa@example.com", "da@example.com")
            .ShouldBe("aa@example.com");
    }

    [Fact]
    public void DefenseAttorneyCreator_WithDaEmail_PromotesDaEmail()
    {
        AttorneyRecipientPromotion.ResolvePrimaryRecipientEmail(
                new[] { Da }, "aa@example.com", "da@example.com")
            .ShouldBe("da@example.com");
    }

    [Fact]
    public void CreatorWithBothRoles_PrefersApplicantAttorney()
    {
        AttorneyRecipientPromotion.ResolvePrimaryRecipientEmail(
                new[] { Da, Aa }, "aa@example.com", "da@example.com")
            .ShouldBe("aa@example.com");
    }

    [Fact]
    public void ApplicantAttorneyCreator_BlankAaEmail_DoesNotPromote()
    {
        AttorneyRecipientPromotion.ResolvePrimaryRecipientEmail(
                new[] { Aa }, "   ", "da@example.com")
            .ShouldBeNull();
    }

    [Fact]
    public void DefenseAttorneyCreator_NullDaEmail_DoesNotPromote()
    {
        AttorneyRecipientPromotion.ResolvePrimaryRecipientEmail(
                new[] { Da }, "aa@example.com", null)
            .ShouldBeNull();
    }

    [Fact]
    public void PatientOrClaimExaminerCreator_DoesNotPromote()
    {
        AttorneyRecipientPromotion.ResolvePrimaryRecipientEmail(
                new[] { "Patient", "Claim Examiner" }, "aa@example.com", "da@example.com")
            .ShouldBeNull();
    }

    [Fact]
    public void InternalStaffCreator_DoesNotPromote()
    {
        // The regression guard: an internal staff booker (no attorney role) must
        // never promote, so the caller keeps To = the patient/booker.
        AttorneyRecipientPromotion.ResolvePrimaryRecipientEmail(
                new[] { "Intake Staff", "Staff Supervisor" }, "aa@example.com", "da@example.com")
            .ShouldBeNull();
    }

    [Fact]
    public void NullRoles_DoesNotPromote()
    {
        AttorneyRecipientPromotion.ResolvePrimaryRecipientEmail(
                null, "aa@example.com", "da@example.com")
            .ShouldBeNull();
    }

    [Fact]
    public void EmptyRoles_DoesNotPromote()
    {
        AttorneyRecipientPromotion.ResolvePrimaryRecipientEmail(
                new string[0], "aa@example.com", "da@example.com")
            .ShouldBeNull();
    }

    [Fact]
    public void RoleMatch_IsCaseInsensitive()
    {
        AttorneyRecipientPromotion.ResolvePrimaryRecipientEmail(
                new[] { "applicant attorney" }, "aa@example.com", null)
            .ShouldBe("aa@example.com");
    }

    [Fact]
    public void PromotedEmail_IsTrimmed()
    {
        AttorneyRecipientPromotion.ResolvePrimaryRecipientEmail(
                new[] { Aa }, "  aa@example.com  ", null)
            .ShouldBe("aa@example.com");
    }
}
