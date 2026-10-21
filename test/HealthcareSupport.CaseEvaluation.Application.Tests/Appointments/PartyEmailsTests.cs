using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// <see cref="PartyEmails.ChangedRoles"/> decides who is a NEW party and therefore gets the packet
/// email after a staff party change (2026-10-21). Getting it wrong either mails people who already
/// have it, or never mails the person who was just added.
/// </summary>
public class PartyEmailsTests
{
    private static PartyEmails.Snapshot Snap(string? patient = null, string? aa = null, string? da = null, string? ce = null)
        => new(patient, aa, da, ce);

    [Fact]
    public void An_added_party_is_new()
    {
        var changes = PartyEmails.ChangedRoles(Snap(), Snap(da: "da@example.test"));

        var change = changes.ShouldHaveSingleItem();
        change.Role.ShouldBe(PartyEmailRole.DefenseAttorney);
        change.Before.ShouldBeNull();
        change.After.ShouldBe("da@example.test");
    }

    [Fact]
    public void A_replaced_party_is_new()
    {
        var changes = PartyEmails.ChangedRoles(Snap(aa: "old@example.test"), Snap(aa: "new@example.test"));

        changes.ShouldHaveSingleItem().Role.ShouldBe(PartyEmailRole.ApplicantAttorney);
    }

    [Fact]
    public void The_same_address_in_other_capitalisation_or_spacing_is_not_new()
    {
        PartyEmails.ChangedRoles(Snap(ce: "CE@Example.test"), Snap(ce: " ce@example.test ")).ShouldBeEmpty();
    }

    [Fact]
    public void A_removal_is_not_a_new_party()
    {
        PartyEmails.ChangedRoles(Snap(da: "da@example.test"), Snap(da: null)).ShouldBeEmpty();
        PartyEmails.ChangedRoles(Snap(da: "da@example.test"), Snap(da: "  ")).ShouldBeEmpty();
    }

    [Fact]
    public void Each_role_is_judged_on_its_own()
    {
        var changes = PartyEmails.ChangedRoles(
            Snap(patient: "p@example.test", aa: "aa@example.test"),
            Snap(patient: "p2@example.test", aa: "aa@example.test", ce: "ce@example.test"));

        changes.Count.ShouldBe(2);
        changes.ShouldContain(c => c.Role == PartyEmailRole.Patient && c.After == "p2@example.test");
        changes.ShouldContain(c => c.Role == PartyEmailRole.ClaimExaminer && c.After == "ce@example.test");
    }
}
