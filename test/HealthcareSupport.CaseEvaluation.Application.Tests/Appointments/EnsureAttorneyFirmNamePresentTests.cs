using Shouldly;
using Volo.Abp;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// BUG-012 Sub-bug 1 (2026-05-22) -- unit tests for
/// <see cref="AppointmentsAppService.EnsureAttorneyFirmNamePresent"/>. The helper centralizes
/// the FirmName-required guard for the two Upsert AA/DA AppService methods; this file tests it
/// in isolation via the InternalsVisibleTo wiring on the Application project.
///
/// <para>2026-08-20 -- the guard now raises a bare <see cref="BusinessException"/> carrying only
/// the code, and ABP resolves the sentence from en.json at the HTTP boundary. These tests
/// therefore assert the CODE and the attached data, not the message: the message is not
/// populated in-process, and whether it resolves is covered by
/// <c>ErrorCodeLocalizationTests</c>. The previous "WithLocalizer" test and its stub localizer
/// were removed along with the optional localizer parameter they existed to exercise.</para>
/// </summary>
public class EnsureAttorneyFirmNamePresentTests
{
    [Theory]
    [InlineData("ApplicantAttorney")]
    [InlineData("DefenseAttorney")]
    public void EnsureAttorneyFirmNamePresent_NullFirmName_Throws(string attorneyRole)
    {
        var ex = Should.Throw<BusinessException>(
            () => AppointmentsAppService.EnsureAttorneyFirmNamePresent(null, attorneyRole));

        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentAttorneyFirmNameRequired);
        ex.Data["AttorneyRole"].ShouldBe(attorneyRole);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void EnsureAttorneyFirmNamePresent_WhitespaceFirmName_Throws(string firmName)
    {
        var ex = Should.Throw<BusinessException>(
            () => AppointmentsAppService.EnsureAttorneyFirmNamePresent(firmName, "ApplicantAttorney"));

        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentAttorneyFirmNameRequired);
    }

    [Theory]
    [InlineData("Bennett & Associates")]
    [InlineData(" Stone Defense LLC ")] // trim is caller's job; non-empty trimmed value passes
    public void EnsureAttorneyFirmNamePresent_PopulatedFirmName_DoesNotThrow(string firmName)
    {
        Should.NotThrow(() =>
            AppointmentsAppService.EnsureAttorneyFirmNamePresent(firmName, "ApplicantAttorney"));
    }
}
