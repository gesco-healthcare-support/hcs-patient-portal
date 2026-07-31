using System;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments;

/// <summary>
/// F5 (2026-05-29) -- pure tests for <see cref="PacketVisibility"/>, the
/// per-role packet allow-list applied at the AppointmentPacketsAppService
/// boundary.
///
/// Acceptance grid:
///   internal role (admin / Intake Staff / Staff Supervisor / IT Admin)
///                                               -> all three kinds
///   Patient                                     -> Patient only
///   Applicant Attorney / Defense Attorney /
///     Claim Examiner                            -> AttorneyClaimExaminer only
///   internal + external mix                     -> all three (internal wins)
///   null / empty / unknown role                 -> none
/// </summary>
public class PacketVisibilityUnitTests
{
    private static readonly PacketKind[] All =
        { PacketKind.Patient, PacketKind.Doctor, PacketKind.AttorneyClaimExaminer };

    [Theory]
    [InlineData("admin")]
    [InlineData("Intake Staff")]
    [InlineData("Staff Supervisor")]
    [InlineData("IT Admin")]
    public void AllowedKinds_InternalRole_ReturnsAllThree(string role)
    {
        PacketVisibility.AllowedKinds(new[] { role }).ShouldBe(All, ignoreOrder: true);
    }

    // IR1 (2026-06-03) retired "Doctor" as an internal persona -- it now falls
    // through to no packet access until a future multi-doctor model re-adds it.
    [Fact]
    public void AllowedKinds_Doctor_ReturnsNone()
    {
        PacketVisibility.AllowedKinds(new[] { "Doctor" }).ShouldBeEmpty();
    }

    [Fact]
    public void AllowedKinds_Patient_ReturnsPatientOnly()
    {
        PacketVisibility.AllowedKinds(new[] { "Patient" }).ShouldBe(new[] { PacketKind.Patient });
    }

    [Theory]
    [InlineData("Applicant Attorney")]
    [InlineData("Defense Attorney")]
    [InlineData("Claim Examiner")]
    public void AllowedKinds_AttorneyOrClaimExaminer_ReturnsAttyCeOnly(string role)
    {
        PacketVisibility.AllowedKinds(new[] { role })
            .ShouldBe(new[] { PacketKind.AttorneyClaimExaminer });
    }

    [Fact]
    public void AllowedKinds_InternalAndExternalMix_InternalWins()
    {
        PacketVisibility.AllowedKinds(new[] { "Patient", "Intake Staff" }).ShouldBe(All, ignoreOrder: true);
    }

    [Fact]
    public void AllowedKinds_NullRoles_ReturnsNone()
    {
        PacketVisibility.AllowedKinds(null).ShouldBeEmpty();
    }

    [Fact]
    public void AllowedKinds_EmptyRoles_ReturnsNone()
    {
        PacketVisibility.AllowedKinds(Array.Empty<string?>()).ShouldBeEmpty();
    }

    [Fact]
    public void AllowedKinds_UnknownRole_ReturnsNone()
    {
        PacketVisibility.AllowedKinds(new[] { "SomeOtherRole" }).ShouldBeEmpty();
    }

    [Fact]
    public void IsAllowed_Patient_CannotAccessDoctorPacket()
    {
        PacketVisibility.IsAllowed(new[] { "Patient" }, PacketKind.Doctor).ShouldBeFalse();
    }

    [Fact]
    public void IsAllowed_Attorney_CanAccessAttyCeButNotPatient()
    {
        PacketVisibility.IsAllowed(new[] { "Defense Attorney" }, PacketKind.AttorneyClaimExaminer).ShouldBeTrue();
        PacketVisibility.IsAllowed(new[] { "Defense Attorney" }, PacketKind.Patient).ShouldBeFalse();
    }

    [Fact]
    public void IsAllowed_Internal_CanAccessDoctorPacket()
    {
        PacketVisibility.IsAllowed(new[] { "Intake Staff" }, PacketKind.Doctor).ShouldBeTrue();
    }
}
