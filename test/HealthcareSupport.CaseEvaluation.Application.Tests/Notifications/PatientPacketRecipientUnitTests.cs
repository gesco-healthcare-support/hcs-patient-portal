using HealthcareSupport.CaseEvaluation.Notifications.Handlers;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Notifications;

/// <summary>
/// 2026-06-11 -- pure tests for the patient-packet recipient fallback
/// (<see cref="PatientPacketEmailHandler.ResolvePatientPacketRecipientEmail"/>).
/// Patient email is now optional; when it is empty the fillable patient packet
/// must fall back to the applicant attorney's address ("if the patient email is
/// not entered, all the communication for the patient is sent to the AA"). The
/// send is skipped only when NEITHER address is set (OLD silent-skip parity).
/// </summary>
public class PatientPacketRecipientUnitTests
{
    [Fact]
    public void PatientEmailPresent_UsesPatientEmail()
    {
        PatientPacketEmailHandler.ResolvePatientPacketRecipientEmail(
                "patient@example.com", "aa@example.com")
            .ShouldBe("patient@example.com");
    }

    [Fact]
    public void PatientEmailNull_FallsBackToApplicantAttorney()
    {
        PatientPacketEmailHandler.ResolvePatientPacketRecipientEmail(
                null, "aa@example.com")
            .ShouldBe("aa@example.com");
    }

    [Fact]
    public void PatientEmailWhitespace_FallsBackToApplicantAttorney()
    {
        PatientPacketEmailHandler.ResolvePatientPacketRecipientEmail(
                "   ", "aa@example.com")
            .ShouldBe("aa@example.com");
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("   ", "   ")]
    [InlineData("", null)]
    public void BothEmpty_ReturnsNull(string? patientEmail, string? aaEmail)
    {
        PatientPacketEmailHandler.ResolvePatientPacketRecipientEmail(patientEmail, aaEmail)
            .ShouldBeNull();
    }
}
