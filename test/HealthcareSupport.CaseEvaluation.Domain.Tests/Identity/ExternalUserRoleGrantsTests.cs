using System.Collections.Generic;
using System.Linq;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Identity;

/// <summary>
/// Pins the external (booking-party) roles' baseline permission grant set. Pure
/// -- reads the static grant generator, no DB. Every external booking role
/// (Patient / Claim Examiner / Applicant Attorney / Defense Attorney) receives
/// this same baseline (<see cref="ExternalUserRoleDataSeedContributor.SeedAsync"/>).
///
/// Security-sensitive: each booking child resource is POSTed to its own
/// permission-gated standalone AppService during submit, so a missing
/// <c>.Create</c> silently breaks that leg of the booking. Pins the full
/// child-create/edit set including the 2026-07-16 Employer parity fix (Employer
/// previously received Default read only, unlike every sibling child).
/// </summary>
public class ExternalUserRoleGrantsTests
{
    private static readonly HashSet<string> Baseline =
        ExternalUserRoleDataSeedContributor.BookingBaselineGrants().ToHashSet();

    [Theory]
    [InlineData("CaseEvaluation.AppointmentInjuryDetails.Create")]
    [InlineData("CaseEvaluation.AppointmentInjuryDetails.Edit")]
    [InlineData("CaseEvaluation.AppointmentPrimaryInsurances.Create")]
    [InlineData("CaseEvaluation.AppointmentPrimaryInsurances.Edit")]
    [InlineData("CaseEvaluation.AppointmentEmployerDetails.Create")]
    [InlineData("CaseEvaluation.AppointmentEmployerDetails.Edit")]
    public void Baseline_grants_child_resource_create_and_edit(string permission) =>
        Baseline.ShouldContain(permission);

    // Employer read (Default) was always present; the parity fix adds Create + Edit.
    [Fact]
    public void Baseline_keeps_employer_default_read() =>
        Baseline.ShouldContain("CaseEvaluation.AppointmentEmployerDetails");
}
