using System;
using System.Collections.Generic;
using HealthcareSupport.CaseEvaluation.Localization;
using Microsoft.Extensions.Localization;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// BUG-012 Sub-bug 1 (2026-05-22) -- unit tests for
/// <see cref="AppointmentsAppService.EnsureAttorneyFirmNamePresent"/>.
/// The helper centralizes the FirmName-required guard for the two
/// Upsert AA/DA AppService methods; this file tests it in isolation
/// via the InternalsVisibleTo wiring on the Application project
/// (same pattern as the BUG-025 size-limit and Phase-8 registration
/// validator tests).
/// </summary>
public class EnsureAttorneyFirmNamePresentTests
{
    [Theory]
    [InlineData("ApplicantAttorney")]
    [InlineData("DefenseAttorney")]
    public void EnsureAttorneyFirmNamePresent_NullFirmName_Throws(string attorneyRole)
    {
        var ex = Should.Throw<UserFriendlyException>(
            () => AppointmentsAppService.EnsureAttorneyFirmNamePresent(null, attorneyRole));

        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentAttorneyFirmNameRequired);
        ex.Data["AttorneyRole"].ShouldBe(attorneyRole);
        // English fallback when localizer is null.
        ex.Message.ShouldBe("Firm Name is required for the attorney section.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void EnsureAttorneyFirmNamePresent_WhitespaceFirmName_Throws(string firmName)
    {
        var ex = Should.Throw<UserFriendlyException>(
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

    [Fact]
    public void EnsureAttorneyFirmNamePresent_WithLocalizer_UsesLocalizedMessage()
    {
        // Verify the optional localizer parameter is consulted when
        // provided -- production callers (the AppService Upsert methods)
        // pass their injected localizer so the SPA banner gets the
        // en.json string, not the English fallback hardcoded above.
        var fake = new FakeAppointmentLocalizer("LOCALIZED-SENTINEL");

        var ex = Should.Throw<UserFriendlyException>(
            () => AppointmentsAppService.EnsureAttorneyFirmNamePresent(null, "ApplicantAttorney", fake));

        ex.Message.ShouldBe("LOCALIZED-SENTINEL");
        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentAttorneyFirmNameRequired);
        ex.Data["AttorneyRole"].ShouldBe("ApplicantAttorney");
    }

    // Minimal IStringLocalizer<CaseEvaluationResource> stub returning the
    // sentinel for the Appointment:AttorneyFirmNameRequired key and a
    // passthrough (ResourceNotFound = true) for any other key. Mirrors
    // the FakeFirmNameLocalizer in ExternalSignupValidatorUnitTests.
    private sealed class FakeAppointmentLocalizer : IStringLocalizer<CaseEvaluationResource>
    {
        private const string Key = "Appointment:AttorneyFirmNameRequired";
        private readonly string _sentinel;

        public FakeAppointmentLocalizer(string sentinel) => _sentinel = sentinel;

        public LocalizedString this[string name] =>
            name == Key
                ? new LocalizedString(name, _sentinel, resourceNotFound: false)
                : new LocalizedString(name, name, resourceNotFound: true);

        public LocalizedString this[string name, params object[] arguments] => this[name];

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) =>
            Array.Empty<LocalizedString>();
    }
}
