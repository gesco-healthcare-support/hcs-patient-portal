using System;
using System.Linq;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// Pins the leak-critical transform behind the relationship-scoped external-user
/// lookup: given the party-email columns of the appointments a caller can
/// already see, return the distinct co-parties (excluding the caller). Pure, no
/// DI. Synthetic non-PHI values.
/// </summary>
public class ExternalCoPartyRulesTests
{
    private static ExternalCoPartyRules.AppointmentParties Appt(
        string? patient = null,
        string? applicantAttorney = null,
        string? defenseAttorney = null,
        string? claimExaminer = null) =>
        new(patient, applicantAttorney, defenseAttorney, claimExaminer);

    [Fact]
    public void Excludes_the_callers_own_email()
    {
        var appts = new[]
        {
            Appt(
                patient: "patient@ex.com",
                applicantAttorney: "caller@ex.com",
                defenseAttorney: "da@ex.com",
                claimExaminer: "ce@ex.com"),
        };

        var result = ExternalCoPartyRules.CollectCoParties("caller@ex.com", appts);

        result.ShouldNotContain(c => string.Equals(c.Email, "caller@ex.com", StringComparison.OrdinalIgnoreCase));
        result.Count.ShouldBe(3);
    }

    [Fact]
    public void Caller_exclusion_is_case_insensitive()
    {
        var appts = new[] { Appt(applicantAttorney: "caller@ex.com", defenseAttorney: "da@ex.com") };

        var result = ExternalCoPartyRules.CollectCoParties("Caller@EX.com", appts);

        result.Select(c => c.Email).ShouldNotContain(e => e.Contains("caller", StringComparison.OrdinalIgnoreCase));
        result.Count.ShouldBe(1);
    }

    [Fact]
    public void Tags_each_co_party_with_its_column_role()
    {
        var appts = new[]
        {
            Appt(
                patient: "p@ex.com",
                defenseAttorney: "da@ex.com",
                claimExaminer: "ce@ex.com"),
        };

        var result = ExternalCoPartyRules.CollectCoParties("caller@ex.com", appts);

        result.ShouldContain(c => c.Email == "p@ex.com" && c.Role == AppointmentAccessRules.PatientRole);
        result.ShouldContain(c => c.Email == "da@ex.com" && c.Role == AppointmentAccessRules.DefenseAttorneyRole);
        result.ShouldContain(c => c.Email == "ce@ex.com" && c.Role == AppointmentAccessRules.ClaimExaminerRole);
    }

    [Fact]
    public void Collapses_duplicates_across_appointments()
    {
        var appts = new[]
        {
            Appt(defenseAttorney: "da@ex.com"),
            Appt(defenseAttorney: "DA@ex.com"),
        };

        var result = ExternalCoPartyRules.CollectCoParties("caller@ex.com", appts);

        result.Count.ShouldBe(1);
        result.Single().Role.ShouldBe(AppointmentAccessRules.DefenseAttorneyRole);
    }

    [Fact]
    public void Same_email_under_two_roles_yields_two_co_parties()
    {
        var appts = new[]
        {
            Appt(applicantAttorney: "firm@ex.com"),
            Appt(defenseAttorney: "firm@ex.com"),
        };

        var result = ExternalCoPartyRules.CollectCoParties("caller@ex.com", appts);

        result.Count.ShouldBe(2);
        result.ShouldContain(c => c.Role == AppointmentAccessRules.ApplicantAttorneyRole);
        result.ShouldContain(c => c.Role == AppointmentAccessRules.DefenseAttorneyRole);
    }

    [Fact]
    public void Ignores_blank_columns()
    {
        var appts = new[] { Appt(patient: "  ", applicantAttorney: null, defenseAttorney: "") };

        var result = ExternalCoPartyRules.CollectCoParties("caller@ex.com", appts);

        result.ShouldBeEmpty();
    }
}
