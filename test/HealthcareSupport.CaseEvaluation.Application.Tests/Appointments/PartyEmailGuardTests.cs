using System.Collections.Generic;
using System.Linq;
using HealthcareSupport.CaseEvaluation.AppointmentAccessors;
using HealthcareSupport.CaseEvaluation.AppointmentClaimExaminers;
using HealthcareSupport.CaseEvaluation.Patients;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>2026-11-13 -- the placeholder-email guard and the list of emails a submit carries. Synthetic data only.</summary>
public class PartyEmailGuardTests
{
    [Fact]
    public void EnsureNoPlaceholder_ThrowsTheCodedErrorForAPlaceholder()
    {
        var error = Should.Throw<BusinessException>(() =>
            PartyEmailGuard.EnsureNoPlaceholder(new[] { "TEST-ok@firm.test", "unknown@gmail.com" }));

        error.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentPlaceholderEmail);
    }

    [Fact]
    public void EnsureNoPlaceholder_AcceptsRealBlankAndNullValues() =>
        Should.NotThrow(() => PartyEmailGuard.EnsureNoPlaceholder(new string?[] { "TEST-ok@firm.test", "", "  ", null }));

    [Theory]
    [InlineData("na@gmail.com", null)]
    [InlineData("na@gmail.com", "unknown@gmail.com")]
    public void EnsureNoPlaceholderIfChanged_RefusesANewOrChangedPlaceholder(string value, string? stored) =>
        Should.Throw<BusinessException>(() => PartyEmailGuard.EnsureNoPlaceholderIfChanged(value, stored))
            .Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentPlaceholderEmail);

    [Theory]
    [InlineData("unknown@gmail.com", "unknown@gmail.com")]
    [InlineData(" Unknown@Gmail.com ", "unknown@gmail.com")]
    [InlineData("TEST-real@firm.test", "unknown@gmail.com")]
    [InlineData("", "unknown@gmail.com")]
    [InlineData(null, null)]
    public void EnsureNoPlaceholderIfChanged_AllowsAnUnchangedOldValueARealOneOrBlank(string? value, string? stored) =>
        Should.NotThrow(() => PartyEmailGuard.EnsureNoPlaceholderIfChanged(value, stored));

    [Fact]
    public void SubmitEmails_ListsEveryPartyEmailTheSubmitCarries()
    {
        var input = new AppointmentSubmitDto
        {
            PatientEmail = "p@x.test",
            ApplicantAttorneyEmail = "aa@x.test",
            DefenseAttorneyEmail = "da@x.test",
            ClaimExaminerEmail = "ce@x.test",
            Patient = new CreatePatientForAppointmentBookingInput { Email = "pp@x.test" },
            ApplicantAttorney = new ApplicantAttorneyDetailsDto { Email = "aar@x.test" },
            DefenseAttorney = new DefenseAttorneyDetailsDto { Email = "dar@x.test" },
            ClaimExaminer = new AppointmentClaimExaminerCreateDto { Email = "cer@x.test" },
            Accessors = new List<AppointmentAccessorCreateDto> { new() { Email = "acc@x.test" } },
        };

        PartyEmailGuard.SubmitEmails(input).ToList().ShouldBe(
            new[]
            {
                "p@x.test", "aa@x.test", "da@x.test", "ce@x.test",
                "pp@x.test", "aar@x.test", "dar@x.test", "cer@x.test", "acc@x.test",
            });
    }
}
