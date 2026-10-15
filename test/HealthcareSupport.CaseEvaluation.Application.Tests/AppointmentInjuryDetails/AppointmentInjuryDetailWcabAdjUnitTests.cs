using System;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails;

/// <summary>
/// CI3 (2026-06-05): ADJ# (WcabAdj) is required per injury. The domain ctor
/// rejects null / whitespace ADJ -- the server backstop behind the DTO
/// [Required] + the UI Validators.required, mirroring the existing claimNumber
/// guard. Plan: docs/plans/2026-06-05-adj-required-per-injury.md.
/// </summary>
public class AppointmentInjuryDetailWcabAdjUnitTests
{
    private static AppointmentInjuryDetail Build(string? wcabAdj) =>
        new(
            id: Guid.NewGuid(),
            appointmentId: Guid.NewGuid(),
            dateOfInjury: new DateTime(2025, 1, 1),
            claimNumber: "CLM-1",
            isCumulativeInjury: false,
            bodyPartsSummary: "Lower back",
            wcabAdj: wcabAdj);

    [Fact]
    public void Ctor_NullWcabAdj_Throws()
    {
        Should.Throw<ArgumentException>(() => Build(null));
    }

    [Fact]
    public void Ctor_WhitespaceWcabAdj_Throws()
    {
        Should.Throw<ArgumentException>(() => Build("   "));
    }

    [Fact]
    public void Ctor_ValidWcabAdj_Succeeds()
    {
        var injury = Build("ADJ-12345");
        injury.WcabAdj.ShouldBe("ADJ-12345");
    }
}
