using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Notifications;

/// <summary>
/// Paralegal-on-behalf-of-attorney (2026-06-10, design D1) -- pure tests for
/// <see cref="BookerCcDispatcher.ResolvePrincipalEmail"/>. The To-anchor of the
/// consolidated message is the booker's PRINCIPAL: the represented attorney when
/// the booker is that side's paralegal, otherwise the booker unchanged. The
/// "unchanged" cases pin the no-regression guarantee for every non-paralegal
/// booking (the rule reduces to identity). Synthetic emails only (HIPAA).
/// </summary>
public class BookerCcDispatcherUnitTests
{
    private const string Booker = "booker@example.com";
    private const string ApplicantParalegal = "ap-paralegal@example.com";
    private const string ApplicantAttorney = "ap-attorney@example.com";
    private const string DefenseParalegal = "def-paralegal@example.com";
    private const string DefenseAttorney = "def-attorney@example.com";

    [Fact]
    public void ResolvePrincipalEmail_ApplicantParalegalBooker_PromotesToApplicantAttorney()
    {
        var principal = BookerCcDispatcher.ResolvePrincipalEmail(
            bookerEmail: ApplicantParalegal,
            applicantParalegalEmail: ApplicantParalegal,
            applicantAttorneyEmail: ApplicantAttorney,
            defenseParalegalEmail: null,
            defenseAttorneyEmail: null);

        principal.ShouldBe(ApplicantAttorney);
    }

    [Fact]
    public void ResolvePrincipalEmail_DefenseParalegalBooker_PromotesToDefenseAttorney()
    {
        var principal = BookerCcDispatcher.ResolvePrincipalEmail(
            bookerEmail: DefenseParalegal,
            applicantParalegalEmail: null,
            applicantAttorneyEmail: null,
            defenseParalegalEmail: DefenseParalegal,
            defenseAttorneyEmail: DefenseAttorney);

        principal.ShouldBe(DefenseAttorney);
    }

    [Fact]
    public void ResolvePrincipalEmail_SelfBookingPatientOrAttorney_ReturnsBookerUnchanged()
    {
        // The booker is not a paralegal on either side -> identity. This is the
        // no-regression guarantee for every existing (non-paralegal) booking.
        var principal = BookerCcDispatcher.ResolvePrincipalEmail(
            bookerEmail: Booker,
            applicantParalegalEmail: ApplicantParalegal,
            applicantAttorneyEmail: ApplicantAttorney,
            defenseParalegalEmail: DefenseParalegal,
            defenseAttorneyEmail: DefenseAttorney);

        principal.ShouldBe(Booker);
    }

    [Fact]
    public void ResolvePrincipalEmail_AttorneySelfBooking_NoParalegalsSet_ReturnsBooker()
    {
        // Attorney books for themselves: no paralegal emails on the appointment.
        var principal = BookerCcDispatcher.ResolvePrincipalEmail(
            bookerEmail: ApplicantAttorney,
            applicantParalegalEmail: null,
            applicantAttorneyEmail: ApplicantAttorney,
            defenseParalegalEmail: null,
            defenseAttorneyEmail: null);

        principal.ShouldBe(ApplicantAttorney);
    }

    [Theory]
    [InlineData("AP-PARALEGAL@EXAMPLE.COM")]
    [InlineData("  ap-paralegal@example.com  ")]
    public void ResolvePrincipalEmail_MatchIsCaseInsensitiveAndTrimmed(string bookerVariant)
    {
        var principal = BookerCcDispatcher.ResolvePrincipalEmail(
            bookerEmail: bookerVariant,
            applicantParalegalEmail: ApplicantParalegal,
            applicantAttorneyEmail: ApplicantAttorney,
            defenseParalegalEmail: null,
            defenseAttorneyEmail: null);

        principal.ShouldBe(ApplicantAttorney);
    }

    [Fact]
    public void ResolvePrincipalEmail_ParalegalBookerButNoAttorneyEmail_FallsBackToBooker()
    {
        // Defensive: a paralegal is recorded but the attorney email is missing.
        // There is no promotion target, so the booker (paralegal) stays the To.
        var principal = BookerCcDispatcher.ResolvePrincipalEmail(
            bookerEmail: ApplicantParalegal,
            applicantParalegalEmail: ApplicantParalegal,
            applicantAttorneyEmail: null,
            defenseParalegalEmail: null,
            defenseAttorneyEmail: null);

        principal.ShouldBe(ApplicantParalegal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolvePrincipalEmail_BlankBooker_ReturnedAsIs(string? booker)
    {
        var principal = BookerCcDispatcher.ResolvePrincipalEmail(
            bookerEmail: booker,
            applicantParalegalEmail: ApplicantParalegal,
            applicantAttorneyEmail: ApplicantAttorney,
            defenseParalegalEmail: null,
            defenseAttorneyEmail: null);

        principal.ShouldBe(booker);
    }
}
